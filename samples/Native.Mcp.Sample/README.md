# Native.Mcp.Sample

A complete, Native-AOT MCP server Lambda built on `Native.Mcp` and **hosted inside
NativeLambdaRouter** (`Native.Mcp.NativeLambdaRouter`) — the recommended Swepay pattern — fronted
by API Gateway HTTP API with a JWT Authorizer. It exposes two tools:

- `ping` — returns `ok` (and the authenticated subject, if present).
- `echo` — echoes a message; requires the `sample-echo` role (defense in depth).

## Build (AOT)

```
dotnet publish -c Release -r linux-arm64
```

Produces a self-contained native `bootstrap` binary suitable for the `provided.al2023` Lambda
runtime (use `linux-x64` for x86-64 functions).

## Try the protocol

`initialize`:

```json
{ "jsonrpc": "2.0", "id": "1", "method": "initialize" }
```

`tools/list`:

```json
{ "jsonrpc": "2.0", "id": "2", "method": "tools/list" }
```

`tools/call`:

```json
{ "jsonrpc": "2.0", "id": "3", "method": "tools/call",
  "params": { "name": "ping", "arguments": {} } }
```

All three are `POST /mcp` with a valid `Authorization: Bearer <jwt>` header (validated by the API
Gateway JWT Authorizer before the Lambda runs).

## How it's wired

See [`Program.cs`](Program.cs): `AddNativeMcpServer` + `AddDiscoveredTools(SampleJsonContext.Default)`
+ `AddNativeMcpTelemetry`, then a `McpRoutedApiGatewayFunction` (from
`Native.Mcp.NativeLambdaRouter`) is the Lambda entry point — the router maps `POST /mcp` to the MCP
dispatcher. [`SampleJsonContext`](SampleJsonContext.cs) covers both the Lambda event types and the
tool input/output types. See [`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md) for the layering.
