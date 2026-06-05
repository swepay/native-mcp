# Native.Mcp.Testing

Test helpers for [`Native.Mcp`](https://github.com/swepay/native-mcp): an in-memory MCP host, a
JSON-RPC test client (no HTTP/Lambda), fluent builders and Shouldly assertion helpers.

This library is reflection-based on purpose and is **not** AOT-constrained — it is test
infrastructure.

## Quickstart

```csharp
await using var host = McpTestHost.CreateBuilder()
    .WithServerInfo("my-mcp", "1.0.0")
    .AddTool<PingTool>()                                  // input/output inferred via reflection
    .ConfigureServices(s => s.AddSingleton<INativeValidator<EchoInput>, EchoInputValidator>())
    .Build();

var client = host.CreateClient();

var response = await client.CallToolAsync("ping", new { }, new McpRequestOptions
{
    CorrelationId = "test-001",
    Roles = ["sample-ping"],
});

response.ShouldBeSuccessful();
response.Envelope!.DataAs<PingOutput>()!.Status.ShouldBe("ok");
```

## Components

- **`McpTestHost` / `McpTestHostBuilder`** — real DI container + dispatcher, no transport.
  `WithMetrics` / `WithToolLogger` / `WithTimeProvider` let you capture telemetry or pin time.
- **`McpTestClient`** — `InitializeAsync`, `ListToolsAsync`, `CallToolAsync`, `SendRawAsync`.
- **Builders** — `McpExecutionContextBuilder` (for unit-testing a tool directly),
  `McpRequestBuilder`, `McpEnvelopeBuilder`.
- **Assertions (Shouldly extensions)** — `result.ShouldBeSuccess()` /
  `result.ShouldBeFailure().ShouldHaveProblemType(...)` on `McpToolResult<T>`;
  `response.ShouldBeSuccessful()` / `response.ShouldBeError().ShouldHaveProblemType(...)` /
  `response.ShouldBeProtocolError(code)` on `McpToolCallResponse`.

## Unit-testing a tool directly

```csharp
var ctx = new McpExecutionContextBuilder().WithRole("sample-ping").Build();
var result = await new PingTool().ExecuteAsync(new PingInput(), ctx, default);
result.ShouldBeSuccess();
```

## License

MIT.
