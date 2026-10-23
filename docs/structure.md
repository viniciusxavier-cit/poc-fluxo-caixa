# Project Structure

**Last updated:** 2026-10-23

## Overview

Clean Architecture + DDD with two independent bounded contexts (`Lancamentos`, `ConsolidadoDiario`), each split into `Domain` → `Application` → `Infrastructure` → `API` layers, plus a `SharedKernel` project holding cross-cutting DDD base types, auth, and HTTP cross-cutting concerns. The two bounded contexts communicate asynchronously through MediatR domain events rather than direct calls.

## Directory Layout

```
fluxocaixa/
├── fluxocaixa.sln                      # References all 11 projects below
├── README.md                           # Architecture diagrams (C4, mermaid), run instructions
├── docs/
│   ├── adr/                            # Architectural Decision Records (ADR-001..ADR-010)
│   ├── tech.md                         # This steering file set
│   ├── structure.md
│   ├── api-standards.md
│   ├── testing-standards.md
│   ├── code-conventions.md
│   └── dod.md
│
├── src/
│   ├── SharedKernel/                    # Cross-cutting DDD + auth + HTTP (netstandard: net8.0)
│   │   ├── Entity.cs                    # Entity<TId> base (identity equality)
│   │   ├── ValueObject.cs               # ValueObject base (structural equality)
│   │   ├── AggregateRoot.cs             # AggregateRoot<TId> : Entity<TId> (domain event buffer)
│   │   ├── IDomainEvent.cs              # : MediatR.INotification
│   │   ├── IUnitOfWork.cs               # CommitAsync(ct)
│   │   ├── TipoLancamento.cs            # Shared enum (Credito=1, Debito=2)
│   │   ├── Auth/                        # JwtSettings, JwtExtensions, TokenService
│   │   ├── Behaviors/                   # ValidationBehavior<TRequest,TResponse>, ValidationException
│   │   └── Http/                        # ValidationExceptionHandler, NotFoundExceptionHandler (IExceptionHandler)
│   │
│   ├── Lancamentos/                     # Bounded context: registering debits/credits
│   │   ├── Lancamentos.Domain/
│   │   │   ├── Entities/Lancamento.cs           # Aggregate Root, private ctor + static Criar() factory
│   │   │   ├── ValueObjects/Dinheiro.cs          # Immutable VO, validates > 0, rounds to 2 decimals
│   │   │   ├── ValueObjects/TipoLancamento.cs
│   │   │   ├── Events/LancamentoCriadoEvent.cs
│   │   │   ├── Events/LancamentoRemovidoEvent.cs
│   │   │   └── Repositories/ILancamentoRepository.cs
│   │   ├── Lancamentos.Application/
│   │   │   ├── Commands/CriarLancamento/        # Command + Handler + Validator, one folder per use case
│   │   │   ├── Commands/RemoverLancamento/
│   │   │   ├── Queries/GetLancamentoPorData/
│   │   │   ├── Queries/GetLancamentoPorId/
│   │   │   └── DTOs/LancamentoDto.cs
│   │   ├── Lancamentos.Infrastructure/
│   │   │   ├── Persistence/LancamentosDbContext.cs
│   │   │   ├── Persistence/Configurations/      # IEntityTypeConfiguration<T> per entity
│   │   │   ├── Repositories/LancamentoRepository.cs
│   │   │   └── UnitOfWork.cs
│   │   └── Lancamentos.API/
│   │       ├── Program.cs                        # DI composition root, middleware pipeline
│   │       ├── Endpoints/LancamentosEndpoints.cs  # Minimal API route group + request records
│   │       └── Properties/launchSettings.json
│   │
│   └── ConsolidadoDiario/                # Bounded context: daily balance read model
│       ├── ConsolidadoDiario.Domain/
│       │   ├── Entities/ConsolidadoDiario.cs     # Aggregate Root, accumulates credits/debits
│       │   └── Repositories/IConsolidadoDiarioRepository.cs
│       ├── ConsolidadoDiario.Application/
│       │   ├── EventHandlers/AtualizarConsolidadoEventHandler.cs   # INotificationHandler<LancamentoCriadoEvent>
│       │   ├── EventHandlers/RecalcularConsolidadoEventHandler.cs # INotificationHandler<LancamentoRemovidoEvent>
│       │   ├── Queries/GetConsolidadoPorData/
│       │   └── DTOs/ConsolidadoDto.cs
│       ├── ConsolidadoDiario.Infrastructure/
│       │   ├── Persistence/ConsolidadoDbContext.cs
│       │   ├── Persistence/Configurations/
│       │   ├── Repositories/ConsolidadoDiarioRepository.cs
│       │   └── ConsolidadoUnitOfWork.cs
│       └── ConsolidadoDiario.API/
│           ├── Program.cs
│           └── Endpoints/ConsolidadoEndpoints.cs  # Includes IMemoryCache read-through (60s TTL)
│
└── tests/
    ├── Lancamentos.UnitTests/
    │   ├── Domain/LancamentoTests.cs
    │   └── Application/CriarLancamentoCommandHandlerTests.cs
    └── ConsolidadoDiario.UnitTests/
        └── Application/AtualizarConsolidadoEventHandlerTests.cs
```

## Module Organization

Each bounded context follows the same four-layer stack, inner to outer:

1. **`{Context}.Domain`** — Aggregate roots, value objects, domain events, repository interfaces (ports). No framework dependencies beyond `SharedKernel`.
2. **`{Context}.Application`** — MediatR commands/queries/handlers organized **one folder per use case** (e.g. `Commands/CriarLancamento/` contains the command record, its handler, and its validator together). Also holds DTOs and cross-context event handlers (`ConsolidadoDiario.Application.EventHandlers` subscribes to `Lancamentos.Domain.Events`).
3. **`{Context}.Infrastructure`** — EF Core `DbContext`, `IEntityTypeConfiguration<T>` classes under `Persistence/Configurations/`, repository implementations, and the `UnitOfWork`/`ConsolidadoUnitOfWork` implementing `IUnitOfWork`.
4. **`{Context}.API`** — ASP.NET Core Minimal API host. `Program.cs` is the DI composition root; `Endpoints/{Context}Endpoints.cs` holds a static `Map{Context}Endpoints(this IEndpointRouteBuilder app)` extension method plus any request DTOs as `sealed record`s.

Note the cross-context reference: `ConsolidadoDiario.Application` project-references `Lancamentos.Domain` (to consume `LancamentoCriadoEvent`/`LancamentoRemovidoEvent`), and `Lancamentos.API` project-references the full `ConsolidadoDiario.Application`/`ConsolidadoDiario.Infrastructure` stack so a single process (`Lancamentos.API`) can host both services' event handlers and both DbContexts. `ConsolidadoDiario.API` can also run standalone for the read endpoint.

## File Naming Conventions

- **Files:** `PascalCase.cs` matching the single public type they contain (`Lancamento.cs`, `CriarLancamentoCommandHandler.cs`)
- **Test files:** `{TypeUnderTest}Tests.cs` (`LancamentoTests.cs`, `CriarLancamentoCommandHandlerTests.cs`)
- **Use-case folders:** PascalCase verb+noun matching the MediatR request name (`CriarLancamento/`, `RemoverLancamento/`, `GetLancamentoPorData/`)
- **Namespaces:** mirror the folder path exactly (`Lancamentos.Application.Commands.CriarLancamento`)

## Import Patterns

- Usings are grouped with BCL/third-party first, then project namespaces, no blank-line separation enforced
- `ImplicitUsings` is enabled in every project, so common `System.*` namespaces are not explicitly imported
- No relative imports (C# doesn't have them); all references are namespace-qualified `using` statements, resolved via project references declared in each `.csproj`

## Configuration Files

- **Root:** `fluxocaixa.sln` (project graph), one `.csproj` per project (dependencies + target framework)
- **Per-API config:** `appsettings.json` / `appsettings.Development.json` inside each `*.API` project (JWT secret, connection strings)
- **Launch profiles:** `Properties/launchSettings.json` inside each `*.API` project (ports 5092 for Lançamentos, 5093 for Consolidado)
- **No** `.editorconfig`, lint config, or CI pipeline files exist in this repo

## Architectural Patterns

- **Dependency Injection:** constructor injection everywhere, registered in each `Program.cs` composition root
- **Interface Segregation:** one repository interface per aggregate (`ILancamentoRepository`, `IConsolidadoDiarioRepository`), defined in `Domain`, implemented in `Infrastructure`
- **Async Operations:** every I/O-bound method is `async`/`Task`-returning and accepts a trailing `CancellationToken cancellationToken` (or `ct`)
- **Error Handling:** domain invariant violations throw `ArgumentException` from factory methods; cross-cutting HTTP translation happens via `IExceptionHandler` implementations in `SharedKernel.Http`
- **Decoupling:** `Lancamentos` never calls `ConsolidadoDiario` directly — it raises a domain event (`AggregateRoot.AddDomainEvent`), persists first, then publishes via `IPublisher.Publish` after `UnitOfWork.CommitAsync` succeeds (see [docs/api-standards.md](api-standards.md) for the failure-isolation contract this implies)
