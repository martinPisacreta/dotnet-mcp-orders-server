using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OrdersMcp;

var builder = Host.CreateApplicationBuilder(args);
// Stdout is reserved exclusively for MCP protocol messages.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

var approvalDirectory = Environment.GetEnvironmentVariable("ORDERS_APPROVAL_DIRECTORY")
    ?? Path.Combine(AppContext.BaseDirectory, "approvals");
builder.Services.AddSingleton(new FileApprovalStore(approvalDirectory));
builder.Services.AddSingleton<OrderService>();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OrderTools>();

await builder.Build().RunAsync();
