using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp;
using Native.Mcp.Sample;

// Sample MCP server Lambda (Native AOT). Fronted by API Gateway HTTP API + JWT Authorizer.
//   POST /mcp  { "jsonrpc": "2.0", "id": "1", "method": "initialize" }
//   POST /mcp  { "jsonrpc": "2.0", "id": "2", "method": "tools/list" }
//   POST /mcp  { "jsonrpc": "2.0", "id": "3", "method": "tools/call",
//                "params": { "name": "ping", "arguments": {} } }

var services = new ServiceCollection();

services.AddNativeMcpServer(options =>
{
    options.ServerName = "swepay-sample-mcp";
    options.ServerVersion = "1.0.0";

    // Registers every IMcpTool in this assembly using the source-generated context (AOT-safe).
    options.AddDiscoveredTools(SampleJsonContext.Default);
});

services.AddNativeMcpTelemetry();

await using var provider = services.BuildServiceProvider();
var mcp = provider.GetRequiredService<McpLambdaHandler>();

var serializer = new SourceGeneratorLambdaJsonSerializer<SampleJsonContext>();

var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext _) => mcp.HandleAsync(request);

await LambdaBootstrapBuilder
    .Create(handler, serializer)
    .Build()
    .RunAsync();
