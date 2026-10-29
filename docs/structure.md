# Project Structure

**Last updated:** 2026-10-29

## Overview

Clean Architecture + DDD with two independent bounded contexts (`Lancamentos`, `ConsolidadoDiario`), each split into `Domain → Application → Infrastructure ← API` layers, plus a shared `SharedKernel` project. See [ADR-001](adr/ADR-001-clean-architecture.md) and [ADR-002](adr/ADR-002-ddd-bounded-contexts.md) for the full rationale.

## Directory Layout

```
fluxocaixa/
├── fluxocaixa.sln                  # Solution file referencing all 11 projects
├── README.md                       # Architecture (C4 diagrams), how-to-run, API reference
├── docs/
│   ├── adr/                        # Architectural Decision Records (ADR-001..011)
│   ├── tech.md / structure.md / api-standards.md / testing-standards.md /
│   │   code-conventions.md / dod.md   # Steering files (this set)
│
├── src/
│   ├── SharedKernel/                      # DDD base types + cross-cutting infra, no API/Infra deps
│   │   ├── Entity.cs                      # Entity<TId> base
│   │   ├── ValueObject.cs                 # ValueObject base (structural equality)
│   │   ├── AggregateRoot.cs               # AggregateRoot<TId> : Entity<TId> + DomainEvents list
│   │   ├── IDomainEvent.cs                # MediatR INotification marker
│   │   ├── IUnitOfWork.cs                 # CommitAsync() abstraction
│   │   ├── TipoLancamento.cs              # Shared enum (Credito=1, Debito=2) — see ADR-011 debt
│   │   ├── Auth/                          # JwtExtensions, JwtSettings, TokenService (see ADR-011 debt)
│   │   ├── Behaviors/                     # ValidationBehavior<,>, ValidationException (MediatR pipeline)
│   │   └── Http/                          # ValidationExceptionHandler, NotFoundExceptionHandler (IExceptionHandler)
│   │
│   ├── Lancamentos/                       # Bounded Context: Lançamentos
│   │   ├── Lancamentos.Domain/
│   │   │   ├── Entities/Lancamento.cs            # Aggregate Root, private ctor + static Criar() factory
│   │   │   ├── ValueObjects/Dinheiro.cs          # Immutable VO, Criar() validates > 0, rounds to 2dp
│   │   │   ├── ValueObjects/TipoLancamento.cs
│   │   │   ├── Events/LancamentoCriadoEvent.cs   # record : IDomainEvent
│   │   │   ├── Events/LancamentoRemovidoEvent.cs
│   │   │   └── Repositories/ILancamentoRepository.cs
│   │   ├── Lancamentos.Application/
│   │   │   ├── Commands/CriarLancamento/         # Command + CommandHandler + Validator (3 files/folder)
│   │   │   ├── Commands/RemoverLancamento/
│   │   │   ├── Queries/GetLancamentoPorData/
│   │   │   ├── Queries/GetLancamentoPorId/
│   │   │   └── DTOs/LancamentoDto.cs
│   │   ├── Lancamentos.Infrastructure/
│   │   │   ├── Persistence/LancamentosDbContext.cs
│   │   │   ├── Persistence/Configurations/       # IEntityTypeConfiguration<T> per entity
│   │   │   ├── Repositories/LancamentoRepository.cs
│   │   │   └── UnitOfWork.cs
│   │   └── Lancamentos.API/
│   │       ├── Program.cs                        # Composition root for BOTH bounded contexts
│   │       ├── Endpoints/LancamentosEndpoints.cs  # + MapAuthEndpoints() for /auth/token
│   │       ├── appsettings.json / appsettings.Development.json
│   │       └── Properties/launchSettings.json
│   │
│   └── ConsolidadoDiario/                 # Bounded Context: Consolidado Diário
│       ├── ConsolidadoDiario.Domain/
│       │   ├── Entities/ConsolidadoDiario.cs     # Aggregate Root; Saldo computed, never stored
│       │   └── Repositories/IConsolidadoDiarioRepository.cs
│       ├── ConsolidadoDiario.Application/
│       │   ├── EventHandlers/AtualizarConsolidadoEventHandler.cs   # consumes LancamentoCriadoEvent
│       │   ├── EventHandlers/RecalcularConsolidadoEventHandler.cs
│       │   ├── Queries/GetConsolidadoPorData/
│       │   └── DTOs/ConsolidadoDto.cs
│       ├── ConsolidadoDiario.Infrastructure/
│       │   ├── Persistence/ConsolidadoDbContext.cs
│       │   ├── Persistence/Configurations/
│       │   ├── Repositories/ConsolidadoDiarioRepository.cs
│       │   └── ConsolidadoUnitOfWork.cs
│       └── ConsolidadoDiario.API/
│           ├── Program.cs                        # Separate, independent composition root
│           ├── Endpoints/ConsolidadoEndpoints.cs
│           └── Properties/launchSettings.json
│
└── tests/
    ├── Lancamentos.UnitTests/
    │   ├── Domain/LancamentoTests.cs
    │   └── Application/
    │       ├── CriarLancamentoCommandHandlerTests.cs
    │       ├── RemoverLancamentoCommandHandlerTests.cs
    │       └── CriarLancamentoCommandValidatorTests.cs
    └── ConsolidadoDiario.UnitTests/
        └── Application/AtualizarConsolidadoEventHandlerTests.cs
```

## Module Organization

### Dependency rule (enforced by project references, see [ADR-001](adr/ADR-001-clean-architecture.md))

| Layer | May depend on | Must NOT depend on |
|---|---|---|
| Domain | SharedKernel only | Application, Infrastructure, API, EF Core |
| Application | Domain | Infrastructure, API, EF Core, HTTP |
| Infrastructure | Application, Domain | API |
| API | Application, Infrastructure | — |

`Lancamentos.API` is the one exception that also references `ConsolidadoDiario.Application`/`.Infrastructure` directly, because it hosts the cross-context event handler at runtime (see [tech.md](tech.md) constraints).

### Per-bounded-context folder pattern

Every `*.Application` project splits strictly into:
- **Commands/`<UseCase>`/** — `<UseCase>Command.cs` (an `IRequest<TResponse>` record), `<UseCase>CommandHandler.cs` (`IRequestHandler<,>`), and optionally `<UseCase>CommandValidator.cs` (`AbstractValidator<T>`)
- **Queries/`<UseCase>`/** — same pattern with `Query`/`QueryHandler`, no validator
- **DTOs/** — flat, one record per DTO, shared by commands, queries and endpoints
- **EventHandlers/** (ConsolidadoDiario only) — `INotificationHandler<TEvent>` implementations

### Cross-context communication

`ConsolidadoDiario.Application` has a direct `ProjectReference` to `Lancamentos.Domain` solely to consume its Domain Event types (`LancamentoCriadoEvent`, `LancamentoRemovidoEvent`). This is treated as an Anti-Corruption Layer in the POC, with a documented (not yet implemented) path to a neutral `Contracts` project — see [ADR-002](adr/ADR-002-ddd-bounded-contexts.md) and [ADR-011](adr/ADR-011-limitacoes-sharedkernel-poc.md).

## File Naming Conventions

- **Files:** `PascalCase.cs` matching the single public type they contain (`CriarLancamentoCommand.cs`, `LancamentoRepository.cs`)
- **Test files:** `<TypeUnderTest>Tests.cs` (`LancamentoTests.cs`, `CriarLancamentoCommandHandlerTests.cs`)
- **Namespaces:** mirror the folder path under the project root, e.g. `Lancamentos.Application.Commands.CriarLancamento`, `SharedKernel.Http`

## Import Patterns

- All projects use C# `ImplicitUsings` — no boilerplate `using System;` etc.
- `using` directives are fully-qualified namespace imports (no project-relative/relative imports exist in C#); one occasional fully-qualified inline reference appears instead of a `using` to avoid an ambiguous name collision (e.g. `ConsolidadoDiario.Domain.Repositories.IConsolidadoUnitOfWork` in `Lancamentos.API/Program.cs`)

## Configuration Files

- **Dependencies:** each project's own `.csproj`; solution-wide list in `fluxocaixa.sln`
- **Runtime config:** `appsettings.json` / `appsettings.Development.json` per API project (connection strings, JWT settings, log levels)
- **Launch profiles:** `Properties/launchSettings.json` per API project (defines local dev ports/environment)
- **No** `.editorconfig`, linter config, or CI pipeline files exist in this repository.

## Architectural Patterns

- **Dependency Injection:** constructor injection throughout; all composition happens in each API's `Program.cs` (top-level statements, no `Startup.cs`)
- **Repository + Unit of Work:** `I<X>Repository` (Domain) / `<X>Repository` (Infrastructure) pairs, each paired with an `IUnitOfWork`/`IConsolidadoUnitOfWork` for `CommitAsync()`
- **Mediator (CQRS):** `IMediator.Send()` for Commands/Queries, `IPublisher.Publish()` for Domain Events — see [ADR-003](adr/ADR-003-cqrs-mediatr.md)
- **Pipeline Behaviors:** `ValidationBehavior<TRequest,TResponse>` registered via `cfg.AddOpenBehavior(typeof(ValidationBehavior<,>))` runs FluentValidation before every handler
- **Async Operations:** all I/O (`DbContext`, repository, mediator) is `async`/`await` with `CancellationToken` threaded through every method signature
- **Error Handling:** custom `IExceptionHandler` implementations (`ValidationExceptionHandler`, `NotFoundExceptionHandler`) registered via `AddExceptionHandler<T>()` + `AddProblemDetails()`, returning RFC 7807 `ProblemDetails`/`ValidationProblemDetails` — see [api-standards.md](api-standards.md)
