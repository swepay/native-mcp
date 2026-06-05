# native-mcp

**MCP (Model Context Protocol) server runtime for .NET 10 Native AOT Lambdas behind API Gateway
HTTP API + JWT Authorizer.**

This repo ships a family of reusable `Native.*` libraries: a transport-agnostic protocol core
plus two hosting packages, a source generator and test helpers.

| Project | Package | Purpose |
|---|---|---|
| [`src/Native.Mcp`](src/Native.Mcp) | `Native.Mcp` | **Core (transport-agnostic):** JSON-RPC dispatch, tool registry, canonical envelope + RFC 9457, validation, telemetry, DI. No Lambda/HTTP dependency. |
| [`src/Native.Mcp.NativeLambdaRouter`](src/Native.Mcp.NativeLambdaRouter) | `Native.Mcp.NativeLambdaRouter` | **Hosting (recommended):** runs MCP inside NativeLambdaRouter (`POST /mcp`), reusing the shared Swepay routing/edge stack. |
| [`src/Native.Mcp.ApiGateway`](src/Native.Mcp.ApiGateway) | `Native.Mcp.ApiGateway` | **Hosting (router-free):** API Gateway HTTP API v2 adapter + Lambda handler. |
| [`src/Native.Mcp.SourceGenerator`](src/Native.Mcp.SourceGenerator) | `Native.Mcp.SourceGenerator` | Roslyn generator: `inputSchema` (JSON Schema 2020-12) + `AddDiscoveredTools`. Bundled into `Native.Mcp`. |
| [`src/Native.Mcp.Testing`](src/Native.Mcp.Testing) | `Native.Mcp.Testing` | In-memory host, JSON-RPC client, builders, FluentAssertions extensions. |

A runnable AOT example (hosted via NativeLambdaRouter) lives in
[`samples/Native.Mcp.Sample`](samples/Native.Mcp.Sample). The architecture and the v1 → v2 changes
are documented in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

> **Naming.** `Native.*` are the reusable libraries. Services that consume them are named
> `Swepay.Mcp.{Product}.{Purpose}` and live in separate repos.

## Start here

The full runtime guide — quickstart, the response/envelope shape, the **error catalog**,
Native-AOT notes and troubleshooting — is in
[**`src/Native.Mcp/README.md`**](src/Native.Mcp/README.md).

## Build & test

```
dotnet build  -c Release
dotnet test   -c Release --settings coverlet.runsettings
```

- **Target:** `net10.0`, Native AOT clean (zero IL2026/IL3050/IL2104 in the libraries).
- **Coverage gate:** line ≥ 85% on `Native.Mcp` and `Native.Mcp.SourceGenerator` (branch target
  ≥ 70%); `Native.Mcp.Testing` is exempt.
- **Stack:** `System.Text.Json` source-gen, `NativeMediator`, `Native.FluentValidation`, xUnit +
  NSubstitute + FluentAssertions.

## Auth model (locked)

The API Gateway HTTP API **JWT Authorizer** validates signature/`exp`/`iss`/`aud` before the
Lambda starts. `Native.Mcp` does **no** cryptographic JWT validation — it extracts the already
validated claims from the request context and exposes them to tools.

## Contributing & security

See [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md).
