using System.Text.Json;

namespace OrdersMcp;

public sealed record Order(string Id, string Status, decimal Total);
public sealed record CancellationApproval(string Action, string OrderId, DateTimeOffset ExpiresAt);

public sealed class FileApprovalStore(string directory)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.GetFullPath(directory);

    public static bool IsSafeId(string id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public bool TryConsume(string orderId)
    {
        if (!IsSafeId(orderId)) return false;
        var source = Path.Combine(root, orderId + ".json");
        var claimed = Path.Combine(root, orderId + ".claimed-" + Guid.NewGuid().ToString("N"));
        var moved = false;
        try
        {
            // Atomic rename prevents two requests from consuming the same file.
            File.Move(source, claimed);
            moved = true;
            var approval = JsonSerializer.Deserialize<CancellationApproval>(
                File.ReadAllText(claimed), JsonOptions);
            return approval is not null &&
                approval.Action == "cancel_order" &&
                approval.OrderId == orderId &&
                approval.ExpiresAt > DateTimeOffset.UtcNow;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
        finally
        {
            if (moved)
            {
                try { File.Delete(claimed); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

public sealed class OrderService(FileApprovalStore approvals)
{
    private readonly object gate = new();
    private readonly Dictionary<string, Order> orders = new(StringComparer.Ordinal)
    {
        ["ORD-1001"] = new("ORD-1001", "pending", 49.90m),
        ["ORD-1002"] = new("ORD-1002", "shipped", 120m)
    };

    public Order[] List()
    {
        lock (gate) return orders.Values.OrderBy(o => o.Id, StringComparer.Ordinal).ToArray();
    }

    public Order Get(string orderId)
    {
        lock (gate) return Find(orderId);
    }

    public Order Cancel(string orderId)
    {
        lock (gate)
        {
            var order = Find(orderId);
            if (order.Status != "pending")
                throw new InvalidOperationException("Only pending orders can be cancelled.");
            if (!approvals.TryConsume(orderId))
                throw new InvalidOperationException("A valid external approval is required. No order was changed.");
            var cancelled = order with { Status = "cancelled" };
            orders[orderId] = cancelled;
            return cancelled;
        }
    }

    private Order Find(string orderId)
    {
        if (!FileApprovalStore.IsSafeId(orderId))
            throw new ArgumentException("Invalid order ID.", nameof(orderId));
        return orders.TryGetValue(orderId, out var order)
            ? order
            : throw new KeyNotFoundException("Order not found.");
    }
}
