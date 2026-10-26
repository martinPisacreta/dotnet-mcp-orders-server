using ModelContextProtocol;
using System.Text.Json;
using OrdersMcp;
using Xunit;

namespace OrdersMcp.Tests;

public sealed class OrderToolsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "orders-mcp-test-" + Guid.NewGuid().ToString("N"));
    private readonly OrderTools tools;

    public OrderToolsTests()
    {
        Directory.CreateDirectory(directory);
        tools = NewTools();
    }

    private OrderTools NewTools() => new(new OrderService(new FileApprovalStore(directory)));

    private void Approve(string fileId, string approvedId, DateTimeOffset expiresAt, string action = "cancel_order")
    {
        var approval = new CancellationApproval(action, approvedId, expiresAt);
        File.WriteAllText(Path.Combine(directory, fileId + ".json"),
            JsonSerializer.Serialize(approval, FileApprovalStore.JsonOptions));
    }

    [Fact]
    public void ListOrdersReturnsSeededOrders()
    {
        Assert.Equal(new[] { "ORD-1001", "ORD-1002" }, tools.ListOrders().Select(o => o.Id));
    }

    [Fact]
    public void GetOrderReturnsRequestedOrder()
    {
        Assert.Equal(new Order("ORD-1001", "pending", 49.90m), tools.GetOrder("ORD-1001"));
    }

    [Fact]
    public void GetOrderRejectsUnknownId()
    {
        Assert.Throws<McpException>(() => tools.GetOrder("ORD-9999"));
    }

    [Fact]
    public void CancelWithoutApprovalLeavesOrderUnchanged()
    {
        var before = tools.GetOrder("ORD-1001");
        var error = Assert.Throws<McpException>(() => tools.CancelOrder("ORD-1001"));
        Assert.Contains("No order was changed", error.Message);
        Assert.Equal(before, tools.GetOrder("ORD-1001"));
    }

    [Fact]
    public void CancelWithExternalApprovalConsumesIt()
    {
        Approve("ORD-1001", "ORD-1001", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Equal("cancelled", tools.CancelOrder("ORD-1001").Status);
        Assert.Equal("cancelled", tools.GetOrder("ORD-1001").Status);
        Assert.Empty(Directory.EnumerateFiles(directory));
        // A fresh service cannot reuse the consumed approval.
        Assert.Throws<McpException>(() => NewTools().CancelOrder("ORD-1001"));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-order")]
    [InlineData("wrong-action")]
    [InlineData("malformed")]
    public void InvalidApprovalNeverChangesOrder(string scenario)
    {
        Approve("ORD-1001", scenario == "wrong-order" ? "ORD-1002" : "ORD-1001",
            scenario == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddMinutes(5),
            scenario == "wrong-action" ? "refund_order" : "cancel_order");
        if (scenario == "malformed")
            File.WriteAllText(Path.Combine(directory, "ORD-1001.json"), "invalid-json");
        Assert.Throws<McpException>(() => tools.CancelOrder("ORD-1001"));
        Assert.Equal("pending", tools.GetOrder("ORD-1001").Status);
    }

    [Fact]
    public void ShippedOrderCannotBeCancelledEvenWithApproval()
    {
        Approve("ORD-1002", "ORD-1002", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.Throws<McpException>(() => tools.CancelOrder("ORD-1002"));
        Assert.Equal("shipped", tools.GetOrder("ORD-1002").Status);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("")]
    public void CancelRejectsUnsafeIds(string orderId)
    {
        Assert.Throws<McpException>(() => tools.CancelOrder(orderId));
        Assert.Equal("pending", tools.GetOrder("ORD-1001").Status);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
