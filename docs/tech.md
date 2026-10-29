# Technology Stack

**Last updated:** 2026-10-29

## Summary

Fluxo de Caixa is a .NET 8 proof-of-concept composed of two independently deployable Minimal API services (`Lancamentos` and `ConsolidadoDiario`) built with Clean Architecture, DDD and CQRS (MediatR), persisting to per-service SQLite databases via EF Core.

## Languages

- **Primary Language:** C# 12 / .NET 8 (`<TargetFramework>net8.0</TargetFramework>` in every `.csproj`)
- **Nullable reference types:** enabled everywhere (`<Nullable>enable</Nullable>`)
- **Implicit usings:** enabled everywhere (`<ImplicitUsings>enable</ImplicitUsings>`)

## Frameworks

### Backend

- **Main Framework:** ASP.NET Core Minimal API (.NET 8), one `Microsoft.NET.Sdk.Web` project per service (`Lancamentos.API`, `ConsolidadoDiario.API`)
- **Mediator / CQRS:** MediatR 12.4.1 — dispatches Commands/Queries (`IMediator.Send`) and Domain Events (`IPublisher.Publish`)
- **Validation:** FluentValidation 11.11.0, wired into the MediatR pipeline via a custom `ValidationBehavior<TRequest, TResponse>` (`src/SharedKernel/Behaviors/`)
- **ORM/Database:** Entity Framework Core 8.0.11 with `Microsoft.EntityFrameworkCore.Sqlite`
- **API Documentation:** Swashbuckle.AspNetCore 6.6.2 (OpenAPI/Swagger UI at `/swagger` per service, Development environment only)
- **Authentication:** `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 + `System.IdentityModel.Tokens.Jwt` 8.3.3, configured once in `SharedKernel.Auth` and reused by both APIs

There is no frontend in this repository — both services are API-only.

## Major Dependencies

### Runtime Dependencies

| Library | Version | Purpose |
|---------|---------|---------|
| MediatR | 12.4.1 | CQRS command/query dispatch and in-process Domain Events |
| FluentValidation | 11.11.0 | Command validation rules (`AbstractValidator<T>`) |
| FluentValidation.DependencyInjectionExtensions | 11.11.0 | Auto-registration of validators (Lancamentos.API only) |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.11 | SQLite provider for EF Core |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | Design-time tooling (`dotnet ef`) |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | JWT Bearer authentication middleware |
| System.IdentityModel.Tokens.Jwt | 8.3.3 | JWT token creation/validation (`TokenService`) |
| Microsoft.AspNetCore.OpenApi | 8.0.11 | Native OpenAPI metadata support |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 8.0.11 | `AddDbContextCheck<T>()` health checks |
| Swashbuckle.AspNetCore | 6.6.2 | Swagger generation and UI |

### Development/Test Dependencies

| Tool | Version | Purpose |
|------|---------|---------|
| xunit | 2.9.2 | Test framework (`[Fact]`, `[Theory]`) |
| xunit.runner.visualstudio | 2.8.2 | Test discovery/execution |
| Microsoft.NET.Test.Sdk | 17.11.1 | .NET test host |
| FluentAssertions | 6.12.1 | Expressive assertions (`.Should()...`) |
| NSubstitute | 5.3.0 | Mocking (`Substitute.For<T>()`, `.Received()`) |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.11 | In-memory DB for `ConsolidadoDiario.UnitTests` event-handler tests |

## Build Tools

- **Package Manager / SDK:** .NET 8 SDK, NuGet via `<PackageReference>`
- **Solution file:** `fluxocaixa.sln` (traditional GUID-based `.sln`, not `dotnet new sln` slnx format) — references all 11 projects (SharedKernel, 2 bounded contexts × 4 layers, 2 test projects)
- **Task Runner:** `dotnet` CLI only — no Makefile, no npm scripts, no custom build scripts

## Development Tools

- **Linter/Formatter:** none configured — no `.editorconfig`, no `Directory.Build.props/.targets`. Code style is whatever `dotnet format` / default Roslyn analyzers enforce plus the conventions documented in [code-conventions.md](code-conventions.md), inferred from existing code.
- **Type Checker:** C# compiler with `Nullable` enabled (compile-time null-safety warnings)
- **Pre-commit Hooks:** none configured in this repository

## Runtime Environment

- **Container:** none — no `Dockerfile` or `docker-compose.yml` present. Services run via `dotnet run`.
- **CI/CD:** none configured — no `.github/workflows` or other pipeline definitions found in this repository.
- **Ports (local dev, from `README.md`):** `Lancamentos.API` → `http://localhost:5092`, `ConsolidadoDiario.API` → `http://localhost:5093`

## External Services

- **Database:** SQLite, one file per bounded context — `lancamentos.db` (Lancamentos.API) and `consolidado.db` (shared connection string, also opened by Lancamentos.API to host the `AtualizarConsolidadoEventHandler`). Created automatically via `Database.EnsureCreatedAsync()` on startup — no EF Core migrations are used.
- **Cache:** `IMemoryCache` (in-process, ASP.NET Core built-in) — used by the Consolidado read endpoint with a 60s TTL. No Redis/distributed cache.
- **Message Queue:** none — Domain Events are dispatched in-process via MediatR's `IPublisher`. See [ADR-004](adr/ADR-004-domain-events-desacoplamento.md) for the documented production migration path to an Outbox Pattern + RabbitMQ/Azure Service Bus.
- **Object Storage:** none

## Constraints

- .NET 8 SDK required (`dotnet --version` → 8.x.x)
- `dotnet run --project <API>` without `--environment Development` disables Swagger if `--urls` is passed explicitly (overrides `launchSettings.json` — see README "Como Rodar Localmente")
- The `Lancamentos.API` process hosts **both** bounded contexts' DbContexts and MediatR handlers (it references `ConsolidadoDiario.Application`/`Infrastructure` directly) so that the `LancamentoCriadoEvent` → `AtualizarConsolidadoEventHandler` flow works in-process; `ConsolidadoDiario.API` is a second, independent process that only exposes the read endpoint.
- **Known secret-management gap:** `src/Lancamentos/Lancamentos.API/appsettings.json` commits a placeholder JWT signing secret (`Jwt:Secret`) in plain text. This is called out explicitly in [ADR-009](adr/ADR-009-seguranca-jwt.md) as a dev-only value to be replaced by an environment variable (`Jwt__Secret`) in production — do not reuse this value anywhere real.
- **Architectural debt tracked in [ADR-011](adr/ADR-011-limitacoes-sharedkernel-poc.md):** `ConsolidadoDiario.Application` has a compile-time `ProjectReference` to `Lancamentos.Domain` (to consume its Domain Events), and `SharedKernel` mixes DDD building blocks with JWT auth infrastructure and the `TipoLancamento` enum. Both are accepted, documented POC trade-offs — do not "fix" them silently as a side effect of an unrelated task.
