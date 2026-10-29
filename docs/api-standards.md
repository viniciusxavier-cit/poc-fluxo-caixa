# API Standards

**Last updated:** 2026-10-29

These conventions are observed from the actual endpoint code in `src/Lancamentos/Lancamentos.API/Endpoints/` and `src/ConsolidadoDiario/ConsolidadoDiario.API/Endpoints/`. Follow them exactly for new endpoints; flag any existing inconsistency rather than silently "fixing" it in an unrelated change.

## REST Conventions

### HTTP Methods

- **GET** — read endpoints (`GetLancamentoPorId`, `ListarLancamentosPorData`, `GetConsolidadoDiario`), no body, dispatched via `IMediator.Send(query)`
- **POST** — create endpoints (`CriarLancamento`, `/auth/token`), body required, dispatched via `IMediator.Send(command)`
- **DELETE** — `RemoverLancamento`, no body, returns `204 No Content`
- **PUT/PATCH** — not used anywhere in this codebase; there is no update-in-place use case today

### URL Patterns (actual routes)

```
POST   /auth/token                       # public — issues a JWT
GET    /health                           # public — health checks

POST   /lancamentos                      # 201 Created
GET    /lancamentos?data=yyyy-MM-dd       # 200 OK — query string, not path segment
GET    /lancamentos/{id:guid}            # 200 OK / 404
DELETE /lancamentos/{id:guid}            # 204 / 404

GET    /consolidado/{data}               # 200 OK / 404 — date is a path segment here, unlike /lancamentos
```

Route params use ASP.NET Core route constraints where applicable (`{id:guid}`). There is no `/api/v1/` prefix anywhere — versioning is not implemented (see Versioning below).

### Status Codes actually produced

- **200 OK** — successful GET
- **201 Created** — `Results.Created($"/lancamentos/{result.Id}", result)` after `POST /lancamentos`
- **204 No Content** — `Results.NoContent()` after `DELETE /lancamentos/{id}`
- **400 Bad Request** — FluentValidation failures, surfaced as `ValidationProblemDetails` by `ValidationExceptionHandler`
- **401 Unauthorized** — `Results.Unauthorized()` from `/auth/token` on bad credentials; also the default ASP.NET Core JWT challenge response on missing/invalid Bearer token
- **404 Not Found** — two different code paths produce this (see "Known inconsistency" below)
- **429 Too Many Requests** — `opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests` when a rate limiter rejects a request
- **500 Internal Server Error** — unhandled exceptions, via the ASP.NET Core default problem-details fallback (`AddProblemDetails()`)

## Request Format

### Headers

```
Content-Type: application/json
Authorization: Bearer {token}
```

No `X-Request-ID` or custom correlation headers are implemented.

### Body example — `POST /lancamentos`

```json
{
  "tipo": 1,
  "valor": 1500.00,
  "descricao": "Venda balcão - cliente João",
  "data": "2025-06-01"
}
```

`tipo` is the `TipoLancamento` enum serialized as its integer value: `1` = Crédito, `2` = Débito.

## Response Format

### Success — single resource (`LancamentoDto`, `ConsolidadoDto`)

Response bodies are flat DTOs (records), not wrapped in a `data` envelope:

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

### Success — list (`GET /lancamentos?data=`)

A bare JSON array (`IReadOnlyList<LancamentoDto>`), **no pagination envelope** — there is no pagination anywhere in this API.

### Error — validation (400, from `ValidationExceptionHandler`)

Standard ASP.NET Core `ValidationProblemDetails` (RFC 7807):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Dados inválidos",
  "status": 400,
  "detail": "Corrija os erros indicados e tente novamente.",
  "errors": {
    "Valor": ["O valor deve ser maior que zero."]
  }
}
```

### Error — not found (404) — **known inconsistency, do not assume one canonical shape**

Two different shapes exist in the current codebase:

1. `RemoverLancamentoCommandHandler` throws `KeyNotFoundException`, caught by `NotFoundExceptionHandler` (`SharedKernel/Http/NotFoundExceptionHandler.cs`), which writes:
   ```json
   { "title": "Não encontrado", "detail": "Lançamento {id} não encontrado.", "status": 404 }
   ```
2. `ConsolidadoEndpoints.MapConsolidadoEndpoints` returns `Results.NotFound(new { mensagem = "..." })` directly from the endpoint lambda:
   ```json
   { "mensagem": "Nenhum consolidado encontrado para 2025-06-01." }
   ```

When adding a new 404 case, prefer shape 1 (throw `KeyNotFoundException`, let `NotFoundExceptionHandler` format it) for consistency with the rest of the Lançamentos API — it is the pattern used by the only other delete/lookup-by-id use case. Do not introduce a third shape.

## Authentication

- **Type:** JWT Bearer, `Microsoft.AspNetCore.Authentication.JwtBearer`, configured once via `services.AddJwtAuthentication(configuration)` in `SharedKernel.Auth` and reused by both `Lancamentos.API` and `ConsolidadoDiario.API`
- **Header:** `Authorization: Bearer {token}`
- **Token issuance:** `POST /auth/token` with hard-coded demo users (`comerciante/senha123`, `admin/admin123`) defined inline in `AuthEndpoints.cs` — explicitly a demo stand-in, not backed by a user store
- **Algorithm / claims:** HMAC-SHA256, issuer `fluxo-caixa`, audience `fluxo-caixa-clients`, 60-minute expiration, zero clock skew (see [ADR-009](adr/ADR-009-seguranca-jwt.md))
- **Endpoint protection:** every business endpoint group calls `.RequireAuthorization()`; `/auth/token` and `/health` call `.AllowAnonymous()` explicitly
- **Swagger:** `c.AddSwaggerJwtSupport()` adds the **Authorize** button so a bearer token can be set once and reused in Swagger UI

## Versioning

- **Not implemented.** There is no `/api/v1/` prefix, no header-based versioning, and no ADR documenting a versioning strategy. Routes are bare (`/lancamentos`, `/consolidado/{data}`). If a new breaking change is needed, raise this gap with the user rather than inventing a scheme unilaterally.

## Rate Limiting (native .NET 8 `System.Threading.RateLimiting`, see [ADR-010](adr/ADR-010-rate-limiting-cache-health.md))

| API | Policy name | Algorithm | Limit | Queue |
|---|---|---|---|---|
| Lancamentos (`/lancamentos` group) | `"api"` | Fixed Window | 100 req/s | 10, oldest-first |
| Consolidado (`/consolidado` group) | `"consolidado"` | Sliding Window | 55 req/s (4 segments) | 10 |

- Applied per-endpoint-group via `.RequireRateLimiting("<policy>")`, not globally.
- Rejections return `429 Too Many Requests` (`opt.RejectionStatusCode`).
- No per-IP/per-client limiting — the limit is process-global.

## Caching

- `GET /consolidado/{data}` caches the `ConsolidadoDto` in `IMemoryCache` for 60 seconds (`CacheTtlSeconds` constant in `ConsolidadoEndpoints.cs`), keyed as `consolidado:{data:yyyy-MM-dd}`.
- No other endpoint caches responses. No distributed cache (Redis) is configured.

## Health Checks

- `GET /health` on both services, `.AllowAnonymous()`, backed by `AddDbContextCheck<T>()` against each service's own `DbContext`.
