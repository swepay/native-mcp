# Native.Mcp.NativeLambdaRouter

Hosts a [`Native.Mcp`](https://www.nuget.org/packages/Native.Mcp) server **inside
[NativeLambdaRouter](https://www.nuget.org/packages/NativeLambdaRouter)** — the recommended way
to run an MCP server in the Swepay Native ecosystem.

The router owns routing, claims extraction, health checks and the Lambda entry point; `Native.Mcp`
owns the JSON-RPC protocol and per-tool authorization. Responsibilities stay cleanly separated and
you reuse the same edge stack as every other Swepay Lambda.

## Responsibility split

| Layer | Owns |
|---|---|
| API Gateway **JWT Authorizer** (edge) | Authentication — validates the JWT (signature/exp/iss/aud) before the Lambda runs. |
| **NativeLambdaRouter** | Routing (`POST /mcp`), claims extraction into `RouteContext`, health checks, the Lambda entry point. |
| **Native.Mcp** | JSON-RPC dispatch (`initialize`/`tools/list`/`tools/call`) and **per-tool** authorization (`context.HasRole`). |

> MCP is a single route. The router can only see the path `/mcp`, not the tool name (which is in
> the JSON-RPC body), so per-tool authorization must live in the tools. The MCP route is registered
> `AllowAnonymous` at the router (the edge Authorizer is the auth gate); the validated claims still
> flow through to the tools.

## Quickstart

```csharp
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Native.Mcp;
using Native.Mcp.NativeLambdaRouter;

var services = new ServiceCollection();
services.AddNativeMcpServer(o =>
{
    o.ServerName = "swepay-native-guard-trial-provisioner";
    o.ServerVersion = "1.0.0";
    o.AddDiscoveredTools(AppJsonContext.Default);   // or AddTool<...>(...)
});
services.AddNativeMcpTelemetry();

await using var provider = services.BuildServiceProvider();
var function = new McpRoutedApiGatewayFunction(provider);   // routes POST /mcp -> dispatcher

var serializer = new SourceGeneratorLambdaJsonSerializer<AppJsonContext>();
var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext ctx) => function.FunctionHandler(req, ctx);
await LambdaBootstrapBuilder.Create(handler, serializer).Build().RunAsync();
```

You do **not** need to register `NativeMediator` — the function supplies a no-op mediator to the
router (dispatch is handled by `Native.Mcp`, not the mediator).

### Custom route path

```csharp
public sealed class MyMcpFunction : McpRoutedApiGatewayFunction
{
    public MyMcpFunction(IServiceProvider sp) : base(sp) { }
    protected override string McpPath => "/v1/mcp";
}
```

## What's in the package

- **`McpRoutedApiGatewayFunction`** — a `RoutedApiGatewayFunction` that maps `POST /mcp` to the MCP
  dispatcher and returns its response verbatim. Override `McpPath` to change the route.
- **`McpRouteContextMapper`** — maps the router's `RouteContext` (claims/headers/body) to
  `McpRequestContext` (claims, raw JWT, `x-correlation-id`, `x-idempotency-key`).

## License

MIT.
