# Changelog

All notable changes to the `Native.Mcp` family are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project uses [SemVer](https://semver.org/).
Releases are tagged `vX.Y.Z`, which triggers the NuGet publish workflow (the package version is
taken from the tag).

## [2.1.0] - 2026-06-05

Role-based authorization (ADR-0006). All packages are versioned together.

### Changed (BREAKING)
- **Authorization model is now role-based (RBAC), mirroring `NativeLambdaRouter`.** Removed
  `McpExecutionContext.HasScope(string)` and the `Scopes` property (OAuth-scope model).
- Telemetry metric `mcp.scope.denied.count` renamed to `mcp.role.denied.count`
  (dimension `required_scope` → `required_role`); `IMcpMetrics.RecordScopeDenied` →
  `RecordRoleDenied`.

### Added
- `McpExecutionContext`: `HasRole(string)`, `HasAnyRole(params string[])`, `RequireRole(string)`,
  `HasClaim(string, string)`, and `Roles` (`IReadOnlyList<string>`).
- `McpForbiddenException` — thrown by `RequireRole`; the dispatcher maps it to the canonical
  `common/forbidden` envelope (HTTP 200, `isError=true`), never an internal error.
- Roles are read from the `role`, `roles`, `cognito:groups` and `groups` claims, in single,
  comma-separated and JSON-array forms (case-sensitive, merged and deduplicated).
- `Native.Mcp.Testing`: `McpExecutionContextBuilder.WithRole/WithoutRole`, `McpRequestOptions.Roles`.

### Docs
- `docs/adr/ADR-0006-authn-authz.md` and `docs/ARCHITECTURE.md` §7 (role model + migration).

## [2.0.0] - 2026-06-04

Transport split + ecosystem integration. All packages versioned together.

### Changed (BREAKING)
- **`Native.Mcp` is now transport-agnostic** — no `Amazon.Lambda.*` dependency. The API Gateway
  adapter and `McpLambdaHandler` moved out of the core; `AddNativeMcpServer` no longer registers
  a Lambda handler.

### Added
- **`Native.Mcp.NativeLambdaRouter`** (recommended hosting): `McpRoutedApiGatewayFunction` hosts
  MCP on `POST /mcp` inside NativeLambdaRouter, reusing routing/claims/edge.
- **`Native.Mcp.ApiGateway`** (router-free hosting): the API Gateway HTTP API v2 adapter +
  `McpLambdaHandler` + `AddNativeMcpApiGatewayHandler()`.

### Tooling
- Tests/assertions migrated from FluentAssertions to **Shouldly**.

## [1.0.0] - 2026-06-03

Initial release: `Native.Mcp` runtime (JSON-RPC dispatch, tool registry, canonical envelope +
RFC 9457, telemetry), `Native.Mcp.SourceGenerator` (bundled), and `Native.Mcp.Testing`.

[2.1.0]: https://github.com/swepay/native-mcp/releases/tag/v2.1.0
[2.0.0]: https://github.com/swepay/native-mcp/releases/tag/v2.0.0
[1.0.0]: https://github.com/swepay/native-mcp/releases/tag/v1.0.0
