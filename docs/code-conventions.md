# Code Conventions

**Last updated:** 2026-10-23

No `.editorconfig`, `.globalconfig`, or `dotnet format` configuration exists in this repository. The conventions below are observed directly from existing code across `src/` and must be followed for consistency in the absence of tooling enforcement.

## Naming Conventions

### Variables and Parameters

- `camelCase` for locals and parameters (`descricao`, `cancellationToken`, `data`)
- Private fields: `_camelCase` with leading underscore (`_repository`, `_unitOfWork`, `_logger`)

### Methods and Properties

- `PascalCase` for all methods and properties (`Criar`, `AddDomainEvent`, `CommitAsync`)
- Domain language is **Portuguese**: method/property/class names use Portuguese business terms (`Lancamento`, `Remover`, `AplicarLancamento`, `Valor`, `Descricao`) — do not translate domain vocabulary to English
- Async methods are suffixed `Async` only when they're infrastructure/I-O methods (`CommitAsync`, `AddAsync`, `GetByDataAsync`); MediatR `Handle` methods are not suffixed since the interface already implies it

### Classes, Interfaces, Records

- `PascalCase` for classes, interfaces, records, enums (`Lancamento`, `ILancamentoRepository`, `CriarLancamentoCommand`, `TipoLancamento`)
- Interfaces prefixed with `I` (`IUnitOfWork`, `IDomainEvent`, `ILancamentoRepository`)
- MediatR requests are `sealed record`s named `{Verb}{Noun}Command`/`{Verb}{Noun}Query` (`CriarLancamentoCommand`, `GetLancamentoPorDataQuery`)
- Handlers are `sealed class`es named `{RequestName}Handler` (`CriarLancamentoCommandHandler`)
- Validators are `sealed class`es named `{RequestName}Validator` extending `AbstractValidator<T>` (`CriarLancamentoCommandValidator`)
- DTOs are `sealed record`s named `{Entity}Dto` (`LancamentoDto`, `ConsolidadoDto`)
- Domain events are `sealed record`s (or classes) named `{Entity}{PastParticiple}Event` (`LancamentoCriadoEvent`, `LancamentoRemovidoEvent`), implementing `SharedKernel.IDomainEvent`

### Constants

- `UPPER_SNAKE_CASE` is not the pattern used here — the one observed constant uses `PascalCase`: `private const int CacheTtlSeconds = 60;`. Follow `PascalCase` for `const` fields to match.

### Files

- `PascalCase.cs`, one file per public type, filename matches the type name exactly (`Dinheiro.cs`, `CriarLancamentoCommandHandler.cs`)

## Code Formatting

- **Indentation:** 4 spaces (standard C# default), no tabs
- **Braces:** Allman style — opening brace on its own new line, for every block including single-statement `if`s that still use braces in multi-line bodies, but single-line guard clauses omit braces (`if (!_validators.Any()) return await next();` style is NOT used here — actual guard clauses in this codebase keep the `if` condition and a one-line throw on separate lines without braces, e.g. in `Dinheiro.Criar`)
- **Quotes:** double quotes (C# strings are always double-quoted)
- **`var` usage:** `var` is used pervasively for local variables when the type is evident from the right-hand side (`var lancamento = new Lancamento { ... };`, `var command = new CriarLancamentoCommand(...);`)
- **Expression-bodied members:** used for trivial one-liners (`public override string ToString() => Valor.ToString("C2");`, `=> _logger = logger;` in constructors)
- **File-scoped namespaces:** every file uses `namespace Foo.Bar;` (semicolon form), not the block `{ }` form

## Type Hints and Annotations

- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` set in every `.csproj` — nullable reference types are enforced by the compiler
- Nullable annotations are used precisely: `Dinheiro.Valor` is non-nullable because `Dinheiro.Criar` guarantees validity; DTOs/results that may not exist use `T?` (`LancamentoDto? result = ...`)
- Non-nullable properties with a private setter that are populated in a factory method use `= null!;` as a placeholder to satisfy the compiler (`public Dinheiro Valor { get; private set; } = null!;`) — this is the established pattern for aggregate properties set exclusively through `Criar`
- Records use positional parameters for immutable data (`public sealed record CriarLancamentoCommand(TipoLancamento Tipo, decimal Valor, string Descricao, DateOnly Data) : IRequest<LancamentoDto>;`)

## Async/Await Patterns

- Every I/O-bound or MediatR-dispatched method is `async Task`/`async Task<T>`/`async ValueTask<bool>` (for `IExceptionHandler`)
- A trailing `CancellationToken cancellationToken` (full name in application/domain layer) or `CancellationToken ct` (abbreviated in endpoint lambdas) parameter is mandatory on every async method and is propagated down to every awaited call
- Always `await` — no `.Result`, `.Wait()`, or fire-and-forget `Task`s anywhere in the codebase
- Endpoint lambdas in Minimal API are `async (Request req, IMediator mediator, CancellationToken ct) => { ... }` — dependencies are resolved via parameter injection, not a constructor

## Error Handling

### Domain validation → exceptions, not result objects

```csharp
public static Lancamento Criar(TipoLancamento tipo, decimal valor, string descricao, DateOnly data)
{
    if (string.IsNullOrWhiteSpace(descricao))
        throw new ArgumentException("A descrição é obrigatória.", nameof(descricao));
    // ...
}
```

- Domain invariants are enforced by throwing `ArgumentException` (with `nameof(param)`) directly from factory methods and value object constructors — never return a `Result<T>`/`Either` type, this codebase doesn't use one
- Application-layer input validation goes through FluentValidation (`AbstractValidator<TCommand>`), wired into the MediatR pipeline via `ValidationBehavior<TRequest,TResponse>` (`src/SharedKernel/Behaviors/ValidationBehavior.cs`), which throws `SharedKernel.Behaviors.ValidationException` — a distinct custom exception type, not FluentValidation's own
- HTTP translation of exceptions is centralized in `SharedKernel.Http.*ExceptionHandler` classes implementing `IExceptionHandler`, registered via `AddExceptionHandler<T>()` — **never** catch-and-translate exceptions inline inside an endpoint lambda or handler body, except for the intentional isolation case below
- **Intentional swallow-and-log**: cross-context event handlers (`AtualizarConsolidadoEventHandler`) catch `Exception` (but always rethrow `OperationCanceledException`) and log via `ILogger.LogError`, because a consolidation failure must never break the originating request. This is the *only* place broad exception catching is acceptable — don't generalize it elsewhere without the same resilience justification.

```csharp
catch (OperationCanceledException)
{
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Falha ao atualizar consolidado para data {Data}. O lançamento {LancamentoId} foi persistido.",
        notification.Data, notification.LancamentoId);
}
```

## Logging

- `Microsoft.Extensions.Logging.ILogger<T>` injected via constructor, no Serilog or structured-logging library currently wired in (Serilog is only mentioned as a future improvement in README.md — don't assume it's present)
- Message templates use named placeholders, not string interpolation: `_logger.LogInformation("Consolidado de {Data} atualizado. Saldo: {Saldo}", notification.Data, consolidado.Saldo);`
- Log levels observed: `LogInformation` for successful state changes, `LogWarning` for client-caused failures (validation, not-found), `LogError` for unexpected/swallowed exceptions with the exception object as the first argument

## Comments and Docstrings

- **No XML doc comments (`///`)** are used anywhere in the codebase — API documentation is instead provided declaratively via Minimal API's `.WithSummary(...)`/`.WithDescription(...)` fluent calls on each endpoint, visible in Swagger
- Inline comments are sparse and only used to label logical sections (`// ── Autenticação JWT ──...` style banner comments in `Program.cs` to separate DI-registration blocks) or to explain a non-obvious invariant (`// Garante que a persistência ocorre ANTES da publicação do evento (resiliência)` in a test)
- Do not add method/class-level doc comments to domain or application code — follow the existing no-docstring convention and let the Minimal API fluent metadata carry endpoint-level documentation

## Anti-Patterns to Avoid

- ❌ **Public setters on aggregate properties** — every aggregate property uses `{ get; private set; }`, mutated only through named domain methods (`Criar`, `Remover`, `AplicarLancamento`), never exposed for direct assignment
- ❌ **Primitive obsession for money** — always wrap monetary values in `Dinheiro`, never pass raw `decimal` through the domain layer
- ❌ **Catching `Exception` broadly** outside the one sanctioned cross-context event-handler case described above
- ❌ **Calling one bounded context's API from another over HTTP** — cross-context communication must go through domain events (see [docs/api-standards.md](api-standards.md)), never a direct project/HTTP call bypassing the event flow
- ❌ **Public constructors on aggregates/value objects** — always `private` constructor + public static factory (`Criar`) that validates before construction
