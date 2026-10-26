# Orders MCP · .NET 8

[![CI](https://github.com/martinPisacreta/dotnet-mcp-orders-server/actions/workflows/ci.yml/badge.svg)](https://github.com/martinPisacreta/dotnet-mcp-orders-server/actions/workflows/ci.yml)

Small C# stdio MCP server using the official NuGet `ModelContextProtocol` SDK (2.3.0). It exposes fictional, in-memory orders through `list_orders`, `get_order` and `cancel_order`. No payment service or external order system is called.

## Build and test

Requires the .NET 8 SDK and NuGet access. Python 3 is needed only for the optional operator command.

```sh
dotnet test OrdersMcp.sln --configuration Release
dotnet build src/OrdersMcp/OrdersMcp.csproj --configuration Release
```

Tests cover all three tool methods, missing orders, cancellation without approval, successful approval consumption, expired or malformed approvals, mismatched action or order, shipped orders and unsafe IDs. Logs go to stderr. Stdout is reserved for MCP messages.

## External approval

Set `ORDERS_APPROVAL_DIRECTORY` to an absolute operator-controlled directory. Without it, the server uses an `approvals` directory beside its assembly. `cancel_order` accepts only an order ID, never an approval flag. An operator independently runs:

```sh
python3 scripts/approve.py /absolute/path/approvals ORD-1001
```

This atomically records a five-minute approval for that action and exact order. The server claims the file by rename and consumes it once. Missing, invalid or expired approval leaves the order unchanged. Only pending orders can be cancelled. Rejections reach the MCP client as tool errors with the reason (for example "A valid external approval is required. No order was changed."), so the model can tell the user what is missing instead of retrying blindly.

No MCP tool creates approvals. Keep this command and directory outside the model's writable filesystem and shell access. This sample does not establish an OS security boundary against an agent running as the same user with arbitrary shell access. The server needs directory permissions to claim and remove approval files.

## Claude Desktop

Build first. Merge this entry into `claude_desktop_config.json`, replacing both paths with absolute paths on the machine running Desktop. On Windows, escape backslashes or use forward slashes. Restart Desktop after saving.

```json
{
  "mcpServers": {
    "orders": {
      "command": "dotnet",
      "args": ["/absolute/path/dotnet-mcp-orders-server/src/OrdersMcp/bin/Release/net8.0/OrdersMcp.dll"],
      "env": {
        "ORDERS_APPROVAL_DIRECTORY": "/absolute/path/approvals"
      }
    }
  }
}
```

## Claude Code

Build first, replace paths, then register the stdio server:

```sh
claude mcp add --transport stdio --env ORDERS_APPROVAL_DIRECTORY=/absolute/path/approvals orders -- dotnet /absolute/path/dotnet-mcp-orders-server/src/OrdersMcp/bin/Release/net8.0/OrdersMcp.dll
```

Use `/mcp` to inspect the connection. These configurations are supplied for setup, not evidence of an interactive Claude client test.

## Boundaries

Orders reset to their seeded values on restart. Approval files persist until consumed or removed, so remove outstanding approvals before restarting a session. Approval consumption and in-memory mutation are not a durable transaction. Production use needs durable order state, authenticated operator approval, audit records and crash recovery. GitHub Actions runs the Release tests on every push.

## License

MIT
