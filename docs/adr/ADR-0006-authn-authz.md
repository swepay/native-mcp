# ADR-0006: Authentication and Authorization for Swepay MCPs

- **Status:** Accepted
- **Date:** 2026-06-03
- **Deciders:** Alex (founder), `@cro` (orchestrator), `@devrel-engineer`, `@compliance-analyst`
- **Technical Story:** Definir o modelo completo de autenticação e autorização para todos os MCPs hospedados no monorepo `swepay-tools-mcp`, garantindo consistência com o ecossistema RBAC do `NativeLambdaRouter` e princípio de menor privilégio.
- **Related ADRs:** ADR-0001 (monorepo), ADR-0007 (planejado: extração de `Native.Authorization`)
- **Supersedes:** discussões anteriores sobre modelo de scopes em RFC do template (v1 e v2) — substituídas por modelo baseado em roles

> **Nota de implementação (`native-mcp`):** este ADR foi implementado em `Native.Mcp` **v2.1.0**.
> `McpExecutionContext.HasScope`/`Scopes` foram removidos; adicionados `HasRole`, `HasAnyRole`,
> `RequireRole`, `HasClaim`, `Roles`. Ver [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) §7.

---

## Context and Problem Statement

Os MCPs Swepay (`Swepay.Mcp.NativeGuard.TrialProvisioner`, `Swepay.Mcp.CaManager.TrialProvisioner`, e futuros) precisam de um modelo de autenticação e autorização que seja consistente com o resto da Swepay (RBAC via `NativeLambdaRouter`), respeite menor privilégio, seja auditável e não reinvente a roda.

A biblioteca `Native.Mcp` ofereceu inicialmente `context.HasScope(...)` baseado em claim `scope` (OAuth/OIDC). Esse modelo **diverge** do RBAC consolidado no router e cria fragmentação interna. Surge a decisão: **qual modelo de autorização adotar nos MCPs e como reaproveitar (ou não) o ecossistema existente?**

## Decision Drivers

- Familiaridade do dev (quem opera o router opera o MCP).
- Consistência arquitetural (toda Swepay usa roles).
- Auditabilidade (single source of truth).
- Velocidade de entrega (MCPs trial em semanas).
- Sustentabilidade futura (refactor planejado, sem dívida permanente).
- Defense in depth (borda / aplicação / domínio).

## Considered Options

1. **Reuso direto da abstração do `NativeLambdaRouter`** — não viável (RBAC acoplado a `RoutedApiGatewayFunction`, sem contrato público desacoplado).
2. **Extração para pacote `Native.Authorization`** — arquitetura limpa, mas semanas de refactor coordenado; bloqueia MCPs trial.
3. **Replicação do contrato no `Native.Mcp`** (chosen, temporary) — `Native.Mcp` implementa própria leitura de roles com API idêntica ao router; dívida documentada para migração futura (Opção 2).

## Decision Outcome

**Opção 3 — Replicação de contrato como ponte para Opção 2 futura.** Quatro partes:

### 4.1 Autenticação (borda)
API Gateway HTTP API com **JWT Authorizer nativo**. Valida assinatura RS256 (JWKS do realm `swepay-internal`), `exp`, `iss`, `aud` antes do Lambda iniciar. Sem JWT válido → 401 no API Gateway. Claims chegam em `requestContext.authorizer.jwt.claims`. Header `Authorization: Bearer <jwt>`.

| Parâmetro | Valor |
|---|---|
| Issuer | `https://nativeguard-{env}.swepay.com.br/realms/swepay-internal` |
| Audience | depende do MCP (ver §4.4) |
| Identity Source | `$request.header.Authorization` |
| Authorizer cache TTL | 300s |

### 4.2 Autorização (aplicação)
RBAC por **roles**, validado **dentro de cada tool** via `context.RequireRole(...)` / `context.HasRole(...)`. A rota `POST /mcp` é `AllowAnonymous` no router (rota única; nome da tool está no body JSON-RPC). Autenticação garantida pelo Authorizer; autorização é responsabilidade da tool.

API (`Native.Mcp` v2.1.0): `HasRole(string)`, `HasAnyRole(params string[])`, `RequireRole(string)` (lança `McpForbiddenException`), `HasClaim(string,string)`.

Leitura de roles (espelha `NativeLambdaRouter`): claims `role`/`roles`/`cognito:groups`/`groups`; formatos single / comma-separated / JSON array; case-sensitive.

### 4.3 Reuso vs replicação
Opção 3 (replicação) agora; **planejar Opção 2** (`Native.Authorization`) no ADR-0007. `Native.Mcp` v2.1.0 ganha própria implementação, deliberadamente espelhada ao router. Lookup de permissions no banco fica fora de escopo. Mitigação: **testes de paridade** garantem mesmo resultado que o router para inputs canônicos.

### 4.4 Modelo de roles dos MCPs trial
**1 role por MCP**, sem granularidade por operação (Fase 1).

| MCP | Service Account | Role | Audience |
|---|---|---|---|
| `Swepay.Mcp.NativeGuard.TrialProvisioner` | `trial-provisioner-ng-bot` | `swepay-ng-trial-provisioner` | `swepay-mcp-ng-trial` |
| `Swepay.Mcp.CaManager.TrialProvisioner` | `trial-provisioner-ca-bot` | `swepay-ca-trial-provisioner` | `swepay-mcp-ca-trial` |

Isolamento cruzado total. Granularidade fina (naming/quota/ownership) é dos **backends**.

### 4.5 Três camadas
1. **Borda** (API Gateway JWT Authorizer) — valida JWT, rejeita antes do Lambda.
2. **Aplicação** (`Native.Mcp` + tool) — rota `AllowAnonymous`; tool valida `HasRole`.
3. **Domínio** (backends) — revalida JWT, policies de naming/quota/ownership, persiste `created_by`.

## Consequences

**Positivas:** familiaridade total; auditoria simplificada; MCPs trial imediatos; isolamento real; path claro para refactor.
**Negativas:** duas implementações de leitura de roles (router + `Native.Mcp`); risco de drift (mitigado por testes de paridade); lookup de permissions não suportado; breaking change em v2.1.0 (remoção de `HasScope`).

## Implementation Notes (resumo)

- **`Native.Mcp` v2.1.0:** remover `HasScope`/`Scopes`; adicionar `HasRole`/`HasAnyRole`/`RequireRole`/`HasClaim`/`Roles`; testes de paridade no CI.
- **SPEC dos MCPs trial:** trocar `HasScope("...")` por `HasRole("swepay-ng-trial-provisioner")`; catálogo de scopes → catálogo de roles; erro `forbidden` cita role.
- **Realm `swepay-internal`:** roles + clients confidential (`client_credentials`) + service accounts + audience mappers; secrets no AWS Secrets Manager (rotation 90d manual no v0).

Error mapping canônico:

| Cenário | Camada | Resposta |
|---|---|---|
| JWT ausente/inválido | Borda | 401 API Gateway |
| Role ausente para a tool | Aplicação (`Native.Mcp`) | 200 + envelope `common/forbidden` |
| Naming policy violada | Domínio | 422 backend → envelope `trial/...-naming-violation` |
| Quota excedida | Domínio | 429 backend → envelope `trial/quota-exceeded` |

## Validation

Revisar se: lookup de permissions virar requisito; aparecer caller read-only (roles `-read`/`-write`); drift nos testes de paridade; `Native.Authorization` (Opção 2) ficar pronta; audit de compliance exigir outro modelo.

## References

- ADR-0001 (monorepo), ADR-0007 (planejado — `Native.Authorization`)
- [NativeLambdaRouter README](https://github.com/swepay/native-lambda-router/blob/main/README.md)
- Skills: `swepay-coding-conventions`, `swepay-mcp-tool-conventions`, `swepay-rfc9457-problem-details`, `swepay-tenant-isolation`, `bacen-lgpd-checklist`

---

*Texto integral do ADR mantido pelos deciders; este arquivo é a cópia versionada no repositório `native-mcp` para rastreabilidade da implementação v2.1.0.*
