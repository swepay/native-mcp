using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp;
using Native.Mcp.NativeLambdaRouter;
using Native.Mcp.Sample;

// Sample MCP server Lambda (Native AOT), hosted inside NativeLambdaRouter — the recommended
// Swepay pattern. The router owns the route (POST /mcp), claims extraction and health checks;
// Native.Mcp owns the JSON-RPC protocol and per-tool authorization. Authentication is the API
// Gateway JWT Authorizer's job (edge).
//   POST /mcp  { "jsonrpc": "2.0", "id": "1", "method": "initialize" }
//   POST /mcp  { "jsonrpc": "2.0", "id": "2", "method": "tools/list" }
//   POST /mcp  { "jsonrpc": "2.0", "id": "3", "method": "tools/call",
//                "params": { "name": "ping", "arguments": {} } }

var services = new ServiceCollection();

services.AddNativeMcpServer(options =>
{
    options.ServerName = "swepay-sample-mcp";
    options.ServerVersion = "2.0.0";

    // Registers every IMcpTool in this assembly using the source-generated context (AOT-safe).
    options.AddDiscoveredTools(SampleJsonContext.Default);
});

services.AddNativeMcpTelemetry();

await using var provider = services.BuildServiceProvider();

// The router-hosted MCP function. Routes POST /mcp -> McpJsonRpcDispatcher.
var function = new McpRoutedApiGatewayFunction(provider);

var serializer = new SourceGeneratorLambdaJsonSerializer<SampleJsonContext>();

var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
    function.FunctionHandler(request, context);

await LambdaBootstrapBuilder
    .Create(handler, serializer)
    .Build()
    .RunAsync();
