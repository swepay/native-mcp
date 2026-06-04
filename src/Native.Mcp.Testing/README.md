# Native.Mcp.Testing

Test helpers for [`Native.Mcp`](https://github.com/swepay/native-mcp): an in-memory MCP host, a
JSON-RPC test client (no HTTP/Lambda), fluent builders and FluentAssertions extensions.

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
    Scopes = ["sample:ping"],
});

response.Should().BeSuccessful();
response.Envelope!.DataAs<PingOutput>()!.Status.Should().Be("ok");
```

## Components

- **`McpTestHost` / `McpTestHostBuilder`** — real DI container + dispatcher, no transport.
  `WithMetrics` / `WithToolLogger` / `WithTimeProvider` let you capture telemetry or pin time.
- **`McpTestClient`** — `InitializeAsync`, `ListToolsAsync`, `CallToolAsync`, `SendRawAsync`.
- **Builders** — `McpExecutionContextBuilder` (for unit-testing a tool directly),
  `McpRequestBuilder`, `McpEnvelopeBuilder`.
- **Assertions** — `result.Should().BeSuccess()/BeFailure().WithProblemType(...)` on
  `McpToolResult<T>`; `response.Should().BeSuccessful()/BeError().WithProblemType(...)` and
  `BeProtocolError(code)` on `McpToolCallResponse`.

## Unit-testing a tool directly

```csharp
var ctx = new McpExecutionContextBuilder().WithScope("sample:ping").Build();
var result = await new PingTool().ExecuteAsync(new PingInput(), ctx, default);
result.Should().BeSuccess();
```

## License

MIT.
