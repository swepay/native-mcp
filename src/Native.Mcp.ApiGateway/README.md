# Native.Mcp.ApiGateway

Direct **API Gateway HTTP API v2** transport for [`Native.Mcp`](https://www.nuget.org/packages/Native.Mcp),
for Lambdas that front an MCP server **without** NativeLambdaRouter.

> Prefer [`Native.Mcp.NativeLambdaRouter`](https://www.nuget.org/packages/Native.Mcp.NativeLambdaRouter)
> for new Swepay services — it reuses the shared routing/edge stack. Use this package when you want
> a minimal, router-free entry point.

## Quickstart

```csharp
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp;
using Native.Mcp.ApiGateway;

var services = new ServiceCollection();
services.AddNativeMcpServer(o =>
{
    o.ServerName = "swepay-sample-mcp";
    o.ServerVersion = "1.0.0";
    o.AddDiscoveredTools(AppJsonContext.Default);
});
services.AddNativeMcpTelemetry();
services.AddNativeMcpApiGatewayHandler();           // registers McpLambdaHandler

await using var provider = services.BuildServiceProvider();
var mcp = provider.GetRequiredService<McpLambdaHandler>();

var serializer = new SourceGeneratorLambdaJsonSerializer<AppJsonContext>();
var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext _) => mcp.HandleAsync(req);
await LambdaBootstrapBuilder.Create(handler, serializer).Build().RunAsync();
```

## What's in the package

- **`McpLambdaHandler`** — entry point: creates a per-invocation DI scope, adapts the proxy event,
  dispatches, and builds the proxy response.
- **`ApiGatewayRequestAdapter`** — decodes the body (base64 aware), extracts the bearer token,
  `x-correlation-id` and `x-idempotency-key`, and the JWT claims.
- **`ApiGatewayClaimsExtractor`** — pulls claims validated by the upstream JWT Authorizer.
- **`ApiGatewayResponseBuilder`** — wraps the dispatcher result into the proxy response.

Authentication is the API Gateway JWT Authorizer's job (edge); per-tool authorization lives in the
tools (`McpExecutionContext.HasRole`).

## License

MIT.
