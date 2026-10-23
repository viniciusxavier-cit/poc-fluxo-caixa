# API Standards

**Last updated:** 2026-10-23

This project exposes two independent ASP.NET Core 8 Minimal API services. Standards below are derived from the existing endpoint definitions in `src/Lancamentos/Lancamentos.API/Endpoints/LancamentosEndpoints.cs` and `src/ConsolidadoDiario/ConsolidadoDiario.API/Endpoints/ConsolidadoEndpoints.cs`.

## Service Topology

| Service | Base URL (local) | Responsibility |
|---|---|---|
| Lançamentos API | `http://localhost:5092` | Register/query/remove cash-flow entries; issues JWT tokens |
| Consolidado Diário API | `http://localhost:5093` | Read-only daily balance, cached 60s |

Each service has its own Swagger UI (`/swagger`, Development environment only) — they are not merged into one OpenAPI document.

## REST Conventions

### HTTP Methods

- **GET** — retrieve resource(s), no body, idempotent
- **POST** — create a resource or perform an action (`/lancamentos`, `/auth/token`), returns `201 Created` with `Results.Created($"/lancamentos/{id}", dto)` for resource creation
- **DELETE** — remove a resource, idempotent, returns `204 No Content`
- `PUT`/`PATCH` are not currently used anywhere in this codebase — do not introduce them without a concrete need

### URL Patterns

```
POST   /auth/token                       # Público — gera token JWT
GET    /health                            # Público — health check (per service)
POST   /lancamentos                       # Bearer — registra débito/crédito
GET    /lancamentos/{id:guid}             # Bearer — busca por ID
GET    /lancamentos?data=yyyy-MM-dd       # Bearer — lista por data (query param, DateOnly binding)
DELETE /lancamentos/{id:guid}             # Bearer — remove
GET    /consolidado/{data}                # Bearer — saldo consolidado (DateOnly route param, cached 60s)
```

- Route groups are defined with `app.MapGroup("/lancamentos")` and shared concerns (`.RequireAuthorization()`, `.RequireRateLimiting("api")`, `.WithTags(...)`) are chained on the group, not repeated per route
- Route parameters use typed constraints (`{id:guid}`) rather than parsing strings manually
- No URL versioning is in use (no `/v1/` segment) — if versioning becomes necessary, follow URL path versioning to match the OpenAPI doc naming already used (`SwaggerDoc("v1", ...)`)

### Status Codes

- **200 OK** — successful GET
- **201 Created** — successful POST that creates a resource (`Results.Created`)
- **204 No Content** — successful DELETE (`Results.NoContent()`)
- **400 Bad Request** — validation failure, produced automatically by `ValidationExceptionHandler` as a `ValidationProblemDetails`
- **401 Unauthorized** — missing/invalid JWT (`RequireAuthorization()`)
- **404 Not Found** — resource doesn't exist; endpoints return `Results.NotFound()` directly, or throw `KeyNotFoundException` which `NotFoundExceptionHandler` converts to a 404 JSON body
- **429 Too Many Requests** — rate limit exceeded (`opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests`)
- **500 Internal Server Error** — unhandled exception, falls through to `UseExceptionHandler()` / ASP.NET Core `AddProblemDetails()` default

Every endpoint declares its possible responses explicitly via `.Produces<T>(status)` / `.ProducesProblem(status)` for accurate OpenAPI output — follow this pattern on any new endpoint.

## Request Format

### Headers

```
Content-Type: application/json
Authorization: Bearer {token}
```

No `X-Request-ID` or correlation header is currently implemented (listed only as a future observability improvement in README.md).

### Body (POST)

Request bodies are plain `sealed record`s declared alongside the endpoint, e.g.:

```csharp
public sealed record CriarLancamentoRequest(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data);
```

```json
{
  "tipo": 1,
  "valor": 1500.00,
  "descricao": "Venda balcão - cliente João",
  "data": "2025-06-01"
}
```

`tipo`: `1` = Crédito, `2` = Débito (see `SharedKernel.TipoLancamento` enum).

## Response Format

### Success Response

DTOs are flat `sealed record`s returned directly as the endpoint result body (no envelope, no `data` wrapper):

```json
{
  "id": "3fa85f64-...",
  "tipo": 1,
  "valor": 1500.00,
  "descricao": "Venda balcão - cliente João",
  "data": "2025-06-01",
  "criadoEm": "2025-06-01T14:32:10Z"
}
```

### List Response

Lists are returned as a bare JSON array (`IReadOnlyList<LancamentoDto>`) — **no pagination envelope exists today**. If pagination becomes necessary, it is a new convention to introduce deliberately, not an existing pattern to follow.

### Error Response

Two shapes currently coexist and both follow this repository's actual handlers — do not invent a third:

**Validation errors (400)** — RFC 7807 `ValidationProblemDetails` via `ValidationExceptionHandler`:
```json
{
  "status": 400,
  "title": "Dados inválidos",
  "detail": "Corrija os erros indicados e tente novamente.",
  "errors": {
    "Descricao": ["A descrição é obrigatória."]
  }
}
```

**Not-found errors (404)** — plain JSON via `NotFoundExceptionHandler`:
```json
{ "title": "Não encontrado", "detail": "...", "status": 404 }
```

New exception-to-HTTP translations should be added as a new `IExceptionHandler` in `SharedKernel.Http`, registered via `builder.Services.AddExceptionHandler<T>()` in `Program.cs`, following the same `TryHandleAsync` pattern — not as inline try/catch in endpoint lambdas.

## Authentication

- **Type:** JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Header:** `Authorization: Bearer {token}`
- **Token issuance:** `POST /auth/token` with `{ "usuario": "...", "senha": "..." }`, implemented via `SharedKernel.Auth.TokenService.GerarToken(usuario, papel)`
- **Token lifetime:** configurable via `Jwt:ExpirationMinutes` (default 60 minutes), no refresh-token mechanism exists
- **Validation:** issuer, audience, lifetime, and signing key are all validated (`ClockSkew = TimeSpan.Zero`); secret comes from `Jwt:Secret` config, overridable via `Jwt__Secret` env var for production
- **Claims:** `ClaimTypes.Name`, `ClaimTypes.Role`, `Jti` — role-based authorization (`comerciante` vs `admin`) is modeled in claims but not yet enforced per-endpoint (`RequireAuthorization()` checks authentication only, no policy/role restriction wired up)
- Public endpoints (`/auth/token`, `/health`) are explicitly exempted with `.AllowAnonymous()`

## Versioning

- No API versioning scheme is implemented. The Swagger doc is pinned to `"v1"` but there is no `/v2` or header/query-param versioning mechanism. If introduced, prefer URL path versioning (`/v1/lancamentos`) to match the existing Swagger doc naming.

## Rate Limiting

- **Mechanism:** ASP.NET Core 8 built-in `AddRateLimiter` with a fixed-window limiter, applied per-route-group via `.RequireRateLimiting("{policyName}")`
- **Lançamentos (`"api"` policy):** 100 permits/second, queue limit 10, oldest-first processing
- **Consolidado (`"consolidado"` policy):** separate named policy — check `ConsolidadoDiario.API/Program.cs` for its exact limits before assuming they match Lançamentos
- **Rejection:** `429 Too Many Requests`, no `X-RateLimit-*` response headers are currently emitted
- Health check endpoints are not rate-limited

## Cross-Service Communication

- Lançamentos and Consolidado Diário do **not** call each other over HTTP. They communicate via **MediatR in-process domain events** (`IPublisher.Publish`), published only *after* the triggering aggregate's own transaction commits (`UnitOfWork.CommitAsync()` → then `Publish`).
- Event handlers in `ConsolidadoDiario.Application.EventHandlers` wrap their work in try/catch and **swallow non-cancellation exceptions**, logging via `ILogger.LogError` instead of rethrowing — this is intentional: a failure to update the consolidated balance must never fail or roll back the originating `/lancamentos` request. Preserve this isolation behavior in any new cross-context event handler.
- This is an in-process MVP substitute for a message broker; README.md documents a planned Outbox + RabbitMQ/Azure Service Bus migration — do not assume durability guarantees beyond at-most-once in-process delivery today.
