# Code Conventions

**Last updated:** 2026-10-29

No `.editorconfig` or formatter config exists in this repo — these conventions are reverse-engineered from the consistent style actually used across `src/` and `tests/`. Follow them exactly; do not introduce a different style "because it's more idiomatic" without checking with the user first.

## Naming Conventions

### Domain language is Portuguese; infrastructure/pattern language is English

The business domain is modeled entirely in Portuguese — type names, method names, property names, exception messages, and test names:

```csharp
public sealed class Lancamento : AggregateRoot<Guid>
{
    public TipoLancamento Tipo { get; private set; }
    public Dinheiro Valor { get; private set; } = null!;
    public static Lancamento Criar(TipoLancamento tipo, decimal valor, string descricao, DateOnly data) { ... }
    public void Remover() { ... }
}
```

Technical/architectural vocabulary stays in English, matching the pattern name: `Command`, `Query`, `Handler`, `Repository`, `UnitOfWork`, `DbContext`, `Endpoints`, `Dto`, `Event`, `Validator`. A type is always `<PortugueseConcept><EnglishPatternSuffix>`, e.g. `CriarLancamentoCommandHandler`, `ILancamentoRepository`, `LancamentoCriadoEvent`, `LancamentoDto`.

Do not mix — never name a new type half-English/half-Portuguese in a way that breaks this split (e.g. avoid `CreateLancamentoCommand` or `LancamentoCreatedEvent`).

### Variables, methods, properties

- **Public members (properties, methods, types):** `PascalCase` (`Valor`, `Criar`, `AplicarLancamento`)
- **Private fields:** `_camelCase` with leading underscore (`_repository`, `_unitOfWork`, `_publisher`, `_dbContext`)
- **Local variables / parameters:** `camelCase` (`valor`, `descricao`, `cancellationToken`)
- **Constants:** `PascalCase`, not `UPPER_SNAKE_CASE` (`private const int CacheTtlSeconds = 60;` in `ConsolidadoEndpoints.cs`) — this follows .NET convention, not the generic cross-language default.

### Files and namespaces

- One public type per file, file named exactly after that type: `Lancamento.cs`, `CriarLancamentoCommandHandler.cs`
- Namespace mirrors the folder path and uses **file-scoped namespace declarations** everywhere (`namespace Lancamentos.Domain.Entities;` — no braces), never the block-scoped `namespace X { ... }` form

## Code Formatting

- **Indentation:** 4 spaces (standard .NET)
- **Braces:** Allman style — opening brace on its own line for types and methods:
  ```csharp
  public static Lancamento Criar(TipoLancamento tipo, decimal valor, string descricao, DateOnly data)
  {
      if (string.IsNullOrWhiteSpace(descricao))
          throw new ArgumentException("A descrição é obrigatória.", nameof(descricao));
      ...
  }
  ```
  Single-statement `if` bodies are written without braces, indented on the next line (see above).
- **Quotes:** double quotes only (C# has no single-quote strings for non-char literals)
- **`using` directives:** at the top of the file, outside the namespace, one per line, no particular alphabetical enforcement observed

## Type Design Patterns

### Aggregate Roots and Value Objects: private constructor + static factory

Every Aggregate Root (`Lancamento`, `ConsolidadoDiario`) and Value Object (`Dinheiro`) follows the same shape: a parameterless/private constructor (for EF Core materialization) plus a `public static <Type> Criar(...)` factory that validates invariants and is the *only* public construction path:

```csharp
private Dinheiro(decimal valor) => Valor = valor;

public static Dinheiro Criar(decimal valor)
{
    if (valor <= 0)
        throw new ArgumentException("O valor deve ser maior que zero.", nameof(valor));

    return new Dinheiro(Math.Round(valor, 2));
}
```

Validation failures throw `ArgumentException` with `nameof(parameter)` and a Portuguese message — never a custom domain exception type, never a silent no-op.

### Commands / Queries / Events / DTOs: records

All MediatR messages and DTOs are `sealed record`s with positional parameters:

```csharp
public sealed record LancamentoCriadoEvent(
    Guid LancamentoId, TipoLancamento Tipo, decimal Valor, DateOnly Data, DateTime OcorridoEm) : IDomainEvent;
```

### Handlers and repositories: `sealed class` with constructor injection

```csharp
public sealed class CriarLancamentoCommandHandler : IRequestHandler<CriarLancamentoCommand, LancamentoDto>
{
    private readonly ILancamentoRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public CriarLancamentoCommandHandler(ILancamentoRepository repository, IUnitOfWork unitOfWork, IPublisher publisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }
}
```

Virtually every class that isn't explicitly meant for inheritance is `sealed`. Do not drop `sealed` on a new type unless it is genuinely meant to be extended.

### Computed properties over stored fields

Derived values are expression-bodied properties, never persisted/cached fields: `public decimal Saldo => TotalCreditos - TotalDebitos;` (and explicitly `builder.Ignore(c => c.Saldo)` in EF configuration — see [ADR-005](adr/ADR-005-sqlite-efcore.md)).

### Required-but-EF-only properties

Domain properties that are always set via the static factory but must satisfy `Nullable` for EF Core's private constructor use `= null!;`:
```csharp
public Dinheiro Valor { get; private set; } = null!;
```
Use this only for genuinely non-nullable domain properties populated by a factory/EF materialization — not as a general escape hatch for null-reference warnings.

## Async/Await

Every method touching a repository, `DbContext`, or `IMediator`/`IPublisher` is `async Task`/`async Task<T>` with a trailing `CancellationToken cancellationToken` (or `ct` in Minimal API lambdas) parameter threaded all the way through:

```csharp
public async Task<LancamentoDto> Handle(CriarLancamentoCommand request, CancellationToken cancellationToken)
{
    var lancamento = Lancamento.Criar(...);
    await _repository.AddAsync(lancamento, cancellationToken);
    await _unitOfWork.CommitAsync(cancellationToken);
    foreach (var @event in lancamento.DomainEvents)
        await _publisher.Publish(@event, cancellationToken);
    lancamento.ClearDomainEvents();
    return new LancamentoDto(...);
}
```

Domain methods with no I/O (`Lancamento.Remover()`, `ConsolidadoDiario.AplicarLancamento(...)`) stay synchronous — do not make a method `async` just because its caller is.

## Error Handling

- Domain invariant violations: `throw new ArgumentException(message, nameof(param))` — synchronous, from the factory/mutator method itself, never wrapped or swallowed in Domain or Application code.
- Missing-resource lookups: `throw new KeyNotFoundException($"...{id}...")` from a command handler (see `RemoverLancamentoCommandHandler`), translated to HTTP 404 centrally by `NotFoundExceptionHandler` in `SharedKernel/Http/`.
- Command validation: FluentValidation validators + the shared `ValidationBehavior<TRequest,TResponse>` MediatR pipeline behavior — handlers themselves never call a validator directly.
- Cross-boundary resilience: the one deliberate `try/catch` that swallows an exception is in `AtualizarConsolidadoEventHandler`, which logs and does **not** rethrow, by design — see [ADR-004](adr/ADR-004-domain-events-desacoplamento.md). This is the only place a broad catch is acceptable; do not generalize this pattern elsewhere without the same resilience justification.
- No project-specific custom exception hierarchy exists (no `DomainException`, no `NotFoundException` class) — stick to built-in BCL exception types (`ArgumentException`, `KeyNotFoundException`) unless a new cross-cutting need justifies adding one.

## Logging

- `ILogger<T>` via constructor injection, used only in a few places (`ValidationExceptionHandler`, `NotFoundExceptionHandler`, the ConsolidadoDiario event handlers). No structured-logging library (Serilog, etc.) is referenced — this is plain `Microsoft.Extensions.Logging` with message templates:
  ```csharp
  _logger.LogWarning("Requisição inválida: {Errors}", string.Join(", ", validationEx.Errors.Select(e => e.ErrorMessage)));
  ```
- Log messages are in Portuguese, matching the rest of the domain language.
- README's "Evolução Futura" section proposes adding Serilog/OpenTelemetry later — do not add them preemptively; treat as a documented future option, not a current standard.

## Comments and Docstrings

- No XML doc comments (`///`) exist anywhere in `src/` or `tests/` — do not add them to new public APIs; Swagger documentation is generated from `.WithSummary()`/`.WithDescription()` on endpoints instead (see [api-standards.md](api-standards.md)), not from XML docs.
- Inline `//` comments are rare and reserved for genuinely non-obvious intent — e.g. explaining *why* an ordering matters (`// Garante que a persistência ocorre ANTES da publicação do evento (resiliência)`) or flagging a deliberate demo shortcut (`// Usuários fictícios para demo — em produção substituir por banco + hash de senha`). Do not add comments that restate what the code already says.

## Anti-Patterns to Avoid (project-specific)

- **Don't** bypass the static `Criar(...)` factory to construct an Aggregate Root or Value Object — always add new validation rules inside the factory, not at the call site.
- **Don't** call a repository/`DbContext` directly from an endpoint lambda — every endpoint must go through `IMediator.Send(...)`, per [ADR-003](adr/ADR-003-cqrs-mediatr.md).
- **Don't** add a synchronous blocking call (`.Result`, `.Wait()`) anywhere in the API/Application layers — every I/O path is already async end-to-end.
- **Don't** introduce a third 404 response shape — see the known inconsistency documented in [api-standards.md](api-standards.md) and converge on the `KeyNotFoundException` → `NotFoundExceptionHandler` pattern for new code.
- **Don't** "fix" the `ConsolidadoDiario.Application → Lancamentos.Domain` project reference or move `Auth`/`TipoLancamento` out of `SharedKernel` as a side effect of unrelated work — both are intentionally-deferred, documented debt ([ADR-011](adr/ADR-011-limitacoes-sharedkernel-poc.md)); raise it as its own change if asked.
