# Security Policy

## Reporting a vulnerability

Report suspected vulnerabilities via coordinated disclosure to **security@swepay.com.br**. Please
do not open public issues for security reports.

## Scope & boundaries

`Native.Mcp` runs **behind** an API Gateway HTTP API JWT Authorizer that validates the JWT
(signature, `exp`, `iss`, `aud`) before the Lambda is invoked. The library:

- does **not** perform cryptographic JWT validation — it only extracts already-validated claims;
- never emits the raw JWT, PII claims (e.g. email, `sub`), exception messages or stack traces in
  any response (LGPD). Unhandled exceptions are mapped to a generic `internal-error` problem;
  full detail is written only to CloudWatch logs;
- treats in-tool scope checks as defense in depth on top of the edge Authorizer.

Tools you build on top of `Native.Mcp` are responsible for not logging or returning the raw JWT
(`McpExecutionContext.RawJwt`) or sensitive claim values.

For general engineering/security questions: **engineering@swepay.com.br**.
