# Native.Mcp

**Transport-agnostic Model Context Protocol (MCP) server runtime for .NET 10 Native AOT.**

`Native.Mcp` is the protocol core: JSON-RPC 2.0 routing (`initialize`, `tools/list`,
`tools/call`), declarative tool registration, source-generated `inputSchema`, input validation
via `Native.FluentValidation`, the canonical Swepay envelope + RFC 9457 problem details, and
CloudWatch EMF telemetry — all reflection-free and AOT-clean. It has **no Lambda/HTTP
dependency**; you choose a hosting package for the transport.

> **Naming.** `Native.Mcp` is the reusable library. Services that *consume* it follow
> `Swepay.Mcp.{Product}.{Purpose}` (e.g. `Swepay.Mcp.NativeGuard.TrialProvisioner`) and live
> in their own repos.

## Hosting packages (pick one)

| Package | Use it when |
|---|---|
| **[`Native.Mcp.NativeLambdaRouter`](https://www.nuget.org/packages/Native.Mcp.NativeLambdaRouter)** | **Recommended.** Host MCP inside NativeLambdaRouter (`POST /mcp`), reusing the shared Swepay routing/edge stack. |
| **[`Native.Mcp.ApiGateway`](https://www.nuget.org/packages/Native.Mcp.ApiGateway)** | Front MCP directly behind API Gateway HTTP API v2, without the router. |

You can also drive the dispatcher from any transport via
`McpJsonRpcDispatcher.DispatchAsync(string? body, McpRequestContext)`.

## What it is / isn't

- **Transport:** Streamable HTTP via a hosting package. No stdio.
- **Auth:** the JWT is validated by the API Gateway JWT Authorizer *before* the Lambda runs.
  This library does **not** verify signatures/exp/iss/aud — it only *extracts* claims; per-tool
  authorization is done in the tools (`McpExecutionContext.HasRole`).
- **v0 scope:** `tools` only (no `resources`/`prompts`), no outbound notifications/streaming.

## Install

```
dotnet add package Native.Mcp
dotnet add package Native.Mcp.NativeLambdaRouter   # or Native.Mcp.ApiGateway
```

`Native.Mcp` bundles `Native.Mcp.SourceGenerator` automatically, so `inputSchema` generation and
the `AddDiscoveredTools` helper are available with no extra reference.

## Quickstart

### 1. Define a tool

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Native.Mcp;

public sealed partial class PingTool : IMcpTool<PingInput, PingOutput>
{
    public static string Name => "ping";
    public static string Description => "Returns ok. Use to verify the server is reachable.";

    public Task<McpToolResult<PingOutput>> ExecuteAsync(
        PingInput input, McpExecutionContext context, CancellationToken cancellationToken)
    {
        // Defense in depth: re-check a role even though the Authorizer already gated the call.
        if (!context.HasRole("sample-ping"))
            return Task.FromResult(McpToolResult<PingOutput>.Failure(
                McpProblems.Forbidden("Required role: sample-ping", context.RequestId)));

        return Task.FromResult(McpToolResult<PingOutput>.Success(new PingOutput { Status = "ok" }));
    }
}

public sealed record PingInput;

public sealed record PingOutput { public required string Status { get; init; } }
```

`partial` lets the source generator add a `public static string InputSchemaJson` (JSON Schema
Draft 2020-12) derived from `PingInput`. Return `McpToolResult.Failure(...)` for expected
errors — never throw for those. Unhandled exceptions are mapped to a leak-free internal error.

### 2. Provide a serializer context (AOT)

```csharp
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
[JsonSerializable(typeof(PingInput))]
[JsonSerializable(typeof(PingOutput))]
public sealed partial class AppJsonContext : JsonSerializerContext;
```

### 3. Bootstrap the Lambda

```csharp
var services = new ServiceCollection();

services.AddNativeMcpServer(options =>
{
    options.ServerName = "swepay-native-guard-trial-provisioner";
    options.ServerVersion = "1.0.0";

    // Option A — explicit (most readable):
    options.AddTool<PingTool, PingInput, PingOutput>(
        AppJsonContext.Default.PingInput, AppJsonContext.Default.PingOutput);

    // Option B — generated discovery (registers every IMcpTool in the assembly):
    // options.AddDiscoveredTools(AppJsonContext.Default);
});

services.AddNativeMcpTelemetry();           // CloudWatch EMF + structured logs

await using var provider = services.BuildServiceProvider();

// Recommended: host inside NativeLambdaRouter (package: Native.Mcp.NativeLambdaRouter).
var function = new McpRoutedApiGatewayFunction(provider);
var serializer = new SourceGeneratorLambdaJsonSerializer<AppJsonContext>();
var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext ctx) => function.FunctionHandler(req, ctx);
await LambdaBootstrapBuilder.Create(handler, serializer).Build().RunAsync();

// Alternative without the router (package: Native.Mcp.ApiGateway):
//   services.AddNativeMcpApiGatewayHandler();
//   var mcp = provider.GetRequiredService<McpLambdaHandler>();
//   var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext _) => mcp.HandleAsync(req);
```

See [`samples/Native.Mcp.Sample`](../../samples/Native.Mcp.Sample) for a complete AOT Lambda
(hosted via NativeLambdaRouter).

## Response shape

Valid JSON-RPC always returns **HTTP 200**. Tool/validation errors are carried inside the
canonical envelope (with `isError: true`) — never as HTTP failures. Only a malformed request
body yields a JSON-RPC `-32700`. Notifications (no `id`) return **HTTP 202** with an empty body.

`tools/call` wraps the canonical Swepay envelope in the MCP tool result:

```json
{
  "jsonrpc": "2.0",
  "id": "step-exec-abc",
  "result": {
    "content": [{ "type": "text", "text": "{ ...envelope... }" }],
    "isError": false
  }
}
```

The envelope:

```json
{
  "success": true,
  "data": { "status": "ok" },
  "error": null,
  "metadata": {
    "requestId": "1",
    "correlationId": "trial_01HX...",
    "timestamp": "2026-06-03T14:30:00.000Z",
    "durationMs": 12,
    "serverName": "swepay-native-guard-trial-provisioner",
    "serverVersion": "1.0.0"
  }
}
```

## Error catalog (emitted by the runtime)

| Situation | Transport | Payload |
|---|---|---|
| Malformed JSON body | HTTP 200 | JSON-RPC error `-32700 Parse error` |
| `jsonrpc != "2.0"` / missing method | HTTP 200 | JSON-RPC error `-32600 Invalid Request` |
| Unknown method | HTTP 200 | JSON-RPC error `-32601 Method not found` |
| `tools/call` missing `name` | HTTP 200 | JSON-RPC error `-32602` |
| `tools/call` unknown tool | HTTP 200 | JSON-RPC error `-32602`, `data: { "tool": "..." }` |
| Notification (no `id`) | HTTP 202 | empty body |
| Arguments don't match schema | HTTP 200 | envelope `isError`, type [`.../common/validation-failed`](https://errors.swepay.com.br/common/validation-failed) |
| `INativeValidator<TInput>` failed | HTTP 200 | envelope `isError`, type `.../common/validation-failed` |
| Tool returned `Failure(...)` | HTTP 200 | envelope `isError`, the tool's problem |
| Tool threw | HTTP 200 | envelope `isError`, type [`.../common/internal-error`](https://errors.swepay.com.br/common/internal-error) — **no** `ex.Message`/stack trace (LGPD) |
| Caller lacks required role (in-tool) | HTTP 200 | envelope `isError`, type [`.../common/forbidden`](https://errors.swepay.com.br/common/forbidden) |
| Authorizer rejected the JWT | HTTP 401 | returned by API Gateway, before the Lambda runs |

Problem-type URIs resolve to the Swepay error catalog at `https://errors.swepay.com.br`. The
runtime never logs the raw JWT, PII claims (email/sub), exception messages or stack traces in any
response. Full exception detail is written to CloudWatch logs internally.

## Telemetry

`AddNativeMcpTelemetry()` emits CloudWatch EMF metrics to stdout — `mcp.tools.call.count`,
`mcp.tools.call.duration_ms`, `mcp.tools.list.count`, `mcp.initialize.count`,
`mcp.protocol.errors.count`, `mcp.validation.failures.count`, `mcp.role.denied.count` — plus a
structured JSON log line per call. Plug in distributed tracing by registering an `IMcpTracer`.

## Native AOT notes

- All runtime serialization goes through source-generated `JsonTypeInfo` — pass your context to
  `AddTool` / `AddDiscoveredTools`.
- The DataAnnotations length attributes (`[MaxLength]`, `[MinLength]`, `[StringLength]`) emit
  `IL2026` under `PublishAot` because their validators use reflection. `Native.Mcp` reads them
  only at compile time, so they are safe — but to keep an AOT build warning-free, express length
  with `[RegularExpression]` or a `Native.FluentValidation` rule, or suppress `IL2026` for those
  members. `[Required]`, `[Description]`, `[Range]`, `[RegularExpression]` are AOT-clean.

## Troubleshooting

- **`tools/call` returns `-32602` for a registered tool** — the `name` must match
  `IMcpTool.Name` exactly (snake_case, case-sensitive).
- **`InvalidOperationException: context does not declare a JsonTypeInfo`** — add
  `[JsonSerializable(typeof(YourInput/Output))]` to the context passed to `AddTool`.
- **Empty `inputSchema` in `tools/list`** — the tool class must be `partial` and top-level for
  the generator to emit `InputSchemaJson`.
- **Validation never runs** — register `INativeValidator<TInput>` in DI; the bridge resolves it
  per call.

## License

MIT.
