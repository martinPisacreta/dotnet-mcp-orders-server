using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace OrdersMcp;

[McpServerToolType]
public sealed class OrderTools(OrderService orders)
{
    [McpServerTool(Name = "list_orders")]
    [Description("List the fictional orders in this server's memory.")]
    public Order[] ListOrders() => orders.List();

    [McpServerTool(Name = "get_order")]
    [Description("Get one fictional order by its exact ID.")]
    public Order GetOrder([Description("Order ID, for example ORD-1001.")] string orderId)
        => Explain(() => orders.Get(orderId));

    [McpServerTool(Name = "cancel_order")]
    [Description("Cancel a pending fictional order only when an operator has independently recorded an unexpired, single-use approval. This tool cannot create approvals.")]
    public Order CancelOrder([Description("Exact ID of the order approved for cancellation.")] string orderId)
        => Explain(() => orders.Cancel(orderId));

    // McpException messages reach the client; other exceptions become a generic error.
    private static Order Explain(Func<Order> action)
    {
        try { return action(); }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            throw new McpException(e is ArgumentException ? "Invalid order ID." : e.Message);
        }
    }
}
