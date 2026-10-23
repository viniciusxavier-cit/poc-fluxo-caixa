# Technology Stack

**Last updated:** 2026-10-23

## Summary

Fluxo de Caixa is a .NET 8 solution composed of two independent microservices (Lançamentos and Consolidado Diário) built with Clean Architecture, DDD, and CQRS via MediatR, each with its own SQLite database.

## Languages

- **Primary Language:** C# 12 (.NET 8)
- **Secondary Languages:** none

## Frameworks

### Backend

- **Main Framework:** ASP.NET Core 8 Minimal API (`Microsoft.NET.Sdk.Web`)
- **ORM/Database:** Entity Framework Core 8.0.11 with SQLite provider
- **API Documentation:** Swashbuckle/OpenAPI 6.6.2 (Swagger UI per service, Development environment only)
- **Mediator/CQRS:** MediatR 12.4.1 (commands, queries, domain events, pipeline behaviors)
- **Validation:** FluentValidation 11.11.0 (`AbstractValidator<T>` + MediatR `ValidationBehavior`)
- **Auth:** `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.11 + `System.IdentityModel.Tokens.Jwt` 8.3.3

### Frontend

- Not applicable — this solution exposes HTTP APIs only, no frontend/UI project exists.

## Major Dependencies

### Runtime Dependencies

| Library | Version | Purpose |
|---|---|---|
| MediatR | 12.4.1 | CQRS commands/queries + in-process domain events |
| FluentValidation | 11.11.0 | Request/command validation |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | JWT bearer authentication |
| System.IdentityModel.Tokens.Jwt | 8.3.3 | JWT token generation/validation |
| Swashbuckle.AspNetCore | 6.6.2 | OpenAPI/Swagger documentation |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.11 | SQLite ORM provider (one DB per service) |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | EF Core design-time tooling (migrations) |
| Microsoft.AspNetCore.OpenApi | 8.0.11 | Minimal API OpenAPI metadata |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 8.0.11 | `/health` endpoint backed by DbContext checks |

### Development/Test Dependencies

| Tool | Version | Purpose |
|------|---------|---------|
| xunit | 2.9.2 | Test framework |
| xunit.runner.visualstudio | 2.8.2 | Test runner integration |
| FluentAssertions | 6.12.1 | Expressive test assertions |
| NSubstitute | 5.3.0 | Mocking in unit tests |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test SDK |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.11 | In-memory DbContext for ConsolidadoDiario tests |

## Build Tools

- **Package Manager:** NuGet (`dotnet restore`)
- **Task Runner:** `dotnet` CLI (no Makefile, no npm scripts)
- **Solution File:** `fluxocaixa.sln` references all 11 projects (6 src, 2 shared/domain support, tests)

## Development Tools

- **Linter/Formatter:** none configured — no `.editorconfig`, no `.globalconfig`, no `dotnet format` config present. Follow the conventions in [docs/code-conventions.md](code-conventions.md) derived from existing code.
- **Type Checker:** C# compiler with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` set in every `.csproj`
- **Pre-commit Hooks:** none configured

## Runtime Environment

- **Container:** none — no Dockerfile or docker-compose.yml present in the repo
- **Orchestration:** not applicable
- **CI/CD:** none configured — no `.github/workflows/`, GitLab CI, or similar found

## External Services

- **Database:** SQLite, one file per service (`lancamentos.db`, `consolidado.db`), created via `Database.EnsureCreatedAsync()` on startup (no migrations applied at runtime)
- **Cache:** `IMemoryCache` (in-process, 60s TTL) on the Consolidado Diário `GET /consolidado/{data}` endpoint only
- **Message Queue:** none — domain events are published in-process via MediatR's `IPublisher`. A future outbox + RabbitMQ/Azure Service Bus migration is documented as a planned evolution in README.md, not yet implemented.
- **Object Storage:** none

## Constraints

- .NET 8 SDK required (`dotnet --version` → 8.x.x)
- Each service (`Lancamentos.API`, `ConsolidadoDiario.API`) runs independently; `Lancamentos.API` also hosts the `ConsolidadoDiario` event handlers in-process (see [docs/structure.md](structure.md))
- Swagger UI is only enabled when `ASPNETCORE_ENVIRONMENT=Development`; passing `--urls` on the CLI resets the environment to `Production` unless `--environment Development` is also passed
- JWT secret is read from `Jwt:Secret` in `appsettings.json`/configuration; production deployments must override it via the `Jwt__Secret` environment variable
