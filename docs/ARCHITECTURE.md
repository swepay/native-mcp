# Native.Mcp — Arquitetura e Mudanças (v1 → v2)

> **Status:** v2.0.0
> **Audiência:** engenharia Swepay e consumidores das libs `Native.*`
> **Resumo:** a v2 separa o **protocolo MCP** (core, transport-agnostic) das **camadas de
> transporte/hospedagem**, e adiciona a integração de primeira classe com o `NativeLambdaRouter`.

---

## 1. Visão geral — como a biblioteca funciona

`Native.Mcp` permite que um Lambda .NET 10 (Native AOT) exponha um **servidor MCP**
(Model Context Protocol, JSON-RPC 2.0) atrás de um **API Gateway HTTP API** com **JWT Authorizer**.

O fluxo de uma chamada `tools/call`:

```
Cliente MCP
   │  POST /mcp  (Authorization: Bearer <jwt>)
   ▼
API Gateway HTTP API  ── JWT Authorizer valida assinatura/exp/iss/aud (EDGE)
   │  evento APIGatewayHttpApiV2ProxyRequest (claims já validados)
   ▼
NativeLambdaRouter  ── roteia POST /mcp, extrai claims p/ RouteContext, health checks
   │  (Native.Mcp.NativeLambdaRouter)  RouteContext → McpRequestContext
   ▼
McpJsonRpcDispatcher (Native.Mcp core)
   ├─ parse JSON-RPC  → initialize | tools/list | tools/call
   ├─ tools/call:  resolve tool → desserializa input (JsonTypeInfo) → valida → executa
   │      └─ tool faz authz fino:  context.HasScope("...")
   ├─ envelope canônico Swepay  (success/data/error/metadata)  + RFC 9457
   └─ telemetria:  EMF (CloudWatch) + log estruturado + subsegmento de trace
   ▼
ApiGatewayResponse (HTTP 200 + envelope)  → API Gateway → Cliente
```

**Princípios:**

- **Reflection-free / AOT-clean.** Toda serialização passa por `JsonSerializerContext`
  source-gen (o consumidor fornece o contexto via `AddTool`/`AddDiscoveredTools`). Zero
  `IL2026/IL3050/IL2104/IL2069` na análise do ILC.
- **Erros nunca viram falha HTTP.** JSON-RPC válido → sempre HTTP 200; erros de tool/validação
  vão no envelope (`isError: true`). Só corpo malformado vira `-32700`. Notificações → HTTP 202.
- **Sem vazamento (LGPD).** Exceções não tratadas viram `internal-error` genérico; nunca
  expõem `ex.Message`, stack trace, JWT cru ou claims sensíveis.

---

## 2. Modelo de responsabilidades (auth/authz)

| Camada | Responsabilidade |
|---|---|
| **API Gateway JWT Authorizer** (edge) | **Autenticação** — valida o JWT antes do Lambda iniciar. |
| **NativeLambdaRouter** | **Roteamento** (`POST /mcp`), extração de claims, health checks, entrypoint Lambda. |
| **Native.Mcp** | **Protocolo** JSON-RPC + **autorização por tool** (`context.HasScope`). |

> ⚠️ MCP é **rota única**. O router só enxerga o path `/mcp`, **não** o nome da tool (que está no
> corpo JSON-RPC). Por isso a autorização **por tool** obrigatoriamente vive nas tools. A rota
> `/mcp` é registrada como `AllowAnonymous` no router (o Authorizer de borda é o portão de
> autenticação); os claims validados continuam fluindo até as tools.

---

## 3. O que mudou da v1 para a v2

A v1 entregava **um** pacote de runtime (`Native.Mcp`) que já incluía o adapter de API Gateway e
o `McpLambdaHandler` — ou seja, o **core dependia de `Amazon.Lambda.*`** e assumia um transporte.

A v2 **isola o protocolo do transporte** e adiciona a integração com o `NativeLambdaRouter`.

### 3.1 Layout de pacotes

| | v1 | v2 |
|---|---|---|
| Core | `Native.Mcp` (protocolo **+ API Gateway adapter + handler**, depende de `Amazon.Lambda.*`) | `Native.Mcp` — **só protocolo**, transport-agnostic, **sem** dependência de Lambda/HTTP |
| Hosting (router) | — | **`Native.Mcp.NativeLambdaRouter`** (novo, recomendado) |
| Hosting (direto) | (embutido no core) | **`Native.Mcp.ApiGateway`** (novo; adapter + `McpLambdaHandler` movidos do core) |
| Source generator | `Native.Mcp.SourceGenerator` (bundled no core) | igual (bundled) |
| Testing | `Native.Mcp.Testing` | igual |

### 3.2 Mudanças concretas (breaking)

- **`Native.Mcp` perdeu** os tipos de transporte: `McpLambdaHandler`, `ApiGatewayRequestAdapter`,
  `ApiGatewayClaimsExtractor`, `ApiGatewayResponseBuilder`. Eles foram para **`Native.Mcp.ApiGateway`**
  (namespace `Native.Mcp.ApiGateway`).
- **`AddNativeMcpServer` não registra mais** o `McpLambdaHandler`. Use:
  - `new McpRoutedApiGatewayFunction(provider)` (pacote NativeLambdaRouter), **ou**
  - `services.AddNativeMcpApiGatewayHandler()` + `McpLambdaHandler` (pacote ApiGateway).
- **`Native.Mcp` não referencia mais `Amazon.Lambda.*`** — um consumidor que use só o core
  (chamando `DispatchAsync` direto) não carrega nenhuma dependência de Lambda.

### 3.3 O que **não** mudou

- Contratos de tool: `IMcpTool<TInput,TOutput>`, `McpToolResult<T>`, `McpExecutionContext`.
- `AddNativeMcpServer` / `AddTool` / `AddDiscoveredTools` / `AddNativeMcpTelemetry`.
- Envelope canônico, `SwepayProblemDetails`, catálogo de erros, telemetria EMF, source generator.
- `Native.Mcp.Testing` (host in-memory + client + assertions).

---

## 4. Por que mudou (racional de arquitetura)

1. **Higiene de dependência.** O protocolo não deve arrastar `Amazon.Lambda.*` para todo
   consumidor. Quem usa o core puro (ou outro transporte) fica enxuto e 100% AOT.
2. **Maturidade de ecossistema (dogfooding).** Rodar o MCP **dentro** do `NativeLambdaRouter`
   prova que o ecossistema Native compõe de ponta a ponta (router → mcp), e padroniza
   roteamento/edge com os demais Lambdas Swepay. Segue o mesmo padrão de
   `Native.FluentValidation.NativeLambdaMediator` (pacote-ponte).
3. **Separação de responsabilidades.** Router cuida de rota/edge; `Native.Mcp` cuida do protocolo
   e da autorização fina por tool. Cada peça evolui na sua cadência.

---

## 5. Guia de migração (v1 → v2)

**Serviço que usa o router (recomendado):**

```diff
  services.AddNativeMcpServer(o => { /* ... */ });
  services.AddNativeMcpTelemetry();
  await using var provider = services.BuildServiceProvider();

- var mcp = provider.GetRequiredService<McpLambdaHandler>();
- var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext _) => mcp.HandleAsync(req);
+ var function = new McpRoutedApiGatewayFunction(provider);                       // novo pacote
+ var handler = (APIGatewayHttpApiV2ProxyRequest req, ILambdaContext ctx) => function.FunctionHandler(req, ctx);

  await LambdaBootstrapBuilder.Create(handler, serializer).Build().RunAsync();
```

Adicione `dotnet add package Native.Mcp.NativeLambdaRouter`.

**Serviço que continua sem router:**

```diff
+ dotnet add package Native.Mcp.ApiGateway
+ // using Native.Mcp.ApiGateway;
+ services.AddNativeMcpApiGatewayHandler();
  var mcp = provider.GetRequiredService<McpLambdaHandler>();   // agora em Native.Mcp.ApiGateway
```

---

## 6. Estrutura final do repositório

```
src/
├── Native.Mcp/                     # protocolo (core, transport-agnostic)
├── Native.Mcp.SourceGenerator/     # gerador (bundled no Native.Mcp)
├── Native.Mcp.ApiGateway/          # transporte direto (API Gateway HTTP API v2)
├── Native.Mcp.NativeLambdaRouter/  # ponte para o NativeLambdaRouter (recomendado)
└── Native.Mcp.Testing/             # helpers de teste
samples/
└── Native.Mcp.Sample/              # Lambda AOT hospedado via NativeLambdaRouter
```

---

## 7. Versionamento

A mudança de layout **remove tipos** de `Native.Mcp` (breaking) → família publicada como
**2.0.0**. O release é dirigido pela tag (`v2.0.0`) — ver `CONTRIBUTING.md`.
