# Testing Standards

**Last updated:** 2026-10-29

See [ADR-007](adr/ADR-007-testes-xunit-nsubstitute.md) for the full rationale behind this stack.

## Testing Framework

- **Framework:** xUnit 2.9.2 (`[Fact]`, `[Theory]`/`[InlineData]`)
- **Assertions:** FluentAssertions 6.12.1 (`.Should()...`)
- **Mocking:** NSubstitute 5.3.0 (`Substitute.For<T>()`, `.Received(n)`, `.DidNotReceive()`, `Arg.Any<T>()`, `.Returns(...)`, `NSubstitute.ExceptionExtensions` for `.ThrowsAsync(...)`)
- **In-memory persistence:** `Microsoft.EntityFrameworkCore.InMemory` 8.0.11 — used only in `ConsolidadoDiario.UnitTests` to exercise the real `DbContext`/repository/`IConsolidadoUnitOfWork` stack
- There are **no integration tests, no E2E tests, and no test project targeting the `*.API` layer** in this repository — only the two `*.UnitTests` projects exist.

## Test Organization

```
tests/
├── Lancamentos.UnitTests/
│   ├── Domain/
│   │   └── LancamentoTests.cs                      # Aggregate + Dinheiro VO, zero mocks
│   └── Application/
│       ├── CriarLancamentoCommandHandlerTests.cs    # NSubstitute for repo/UoW/publisher
│       ├── RemoverLancamentoCommandHandlerTests.cs
│       └── CriarLancamentoCommandValidatorTests.cs  # FluentValidation rule tests
└── ConsolidadoDiario.UnitTests/
    └── Application/
        └── AtualizarConsolidadoEventHandlerTests.cs # EF Core InMemory, covers 2 event handlers + the Aggregate
```

One test class per production type under test, named `<TypeUnderTest>Tests.cs`, placed in the mirror of its namespace (`Domain/` vs `Application/`). `ConsolidadoDiario.UnitTests` does not have a separate `Domain/` folder — its one domain test (`ConsolidadoDiario_SaldoECalculado_NaoArmazenado`) lives alongside the event-handler tests in the same file because the Aggregate is exercised through the same InMemory fixture.

## Test Naming Convention

Pattern: `Metodo_CondicaoOuCenario_ResultadoEsperado` (Portuguese, present in every test across both projects):

```
Criar_ComDadosValidos_DeveCriarLancamento
Criar_ComValorInvalido_DeveLancarExcecao
Handle_DeveAdicionarLancamentoNoRepositorio
Handle_PrimeiroLancamento_DeveCriarConsolidado
RecalcularHandler_EstornoMaiorQueTotal_NaoDeveResultarEmValorNegativo
```

Use this exact three-part naming (method/action, scenario, expectation) for new tests — do not switch to English or to a different convention within these projects.

## Test Structure

Arrange/Act/Assert, without explicit `// Arrange` / `// Act` / `// Assert` comments — blocks are separated by a blank line only. Comments are reserved for non-obvious intent (e.g. `// Garante que a persistência ocorre ANTES da publicação do evento (resiliência)`).

```csharp
[Fact]
public async Task Handle_DeveFazerCommit()
{
    var command = new CriarLancamentoCommand(TipoLancamento.Credito, 200m, "TED recebida", DateOnly.FromDateTime(DateTime.Today));

    await _handler.Handle(command, CancellationToken.None);

    await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
}
```

## Domain tests — zero mocks

Aggregate/Value Object invariants are tested directly, with no test doubles, because the Domain layer has no external dependencies by construction (see [ADR-001](adr/ADR-001-clean-architecture.md)):

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(-100)]
public void Criar_ComValorInvalido_DeveLancarExcecao(decimal valor)
{
    var act = () => Lancamento.Criar(TipoLancamento.Debito, valor, "Pagamento", DateOnly.FromDateTime(DateTime.Today));

    act.Should().Throw<ArgumentException>().WithMessage("*maior que zero*");
}
```

`WithMessage("*fragment*")` wildcard matching is the standard way to assert on exception messages — tests do not assert the full exact string.

## Application/handler tests — NSubstitute

Dependencies are declared as `readonly` fields initialized inline with `Substitute.For<T>()`, and the handler under test is constructed in the test class constructor (xUnit's implicit per-test setup):

```csharp
public sealed class CriarLancamentoCommandHandlerTests
{
    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly CriarLancamentoCommandHandler _handler;

    public CriarLancamentoCommandHandlerTests()
    {
        _handler = new CriarLancamentoCommandHandler(_repository, _unitOfWork, _publisher);
    }
}
```

Behavior assertions use `.Received(1)` / `.DidNotReceive()` with `Arg.Any<T>()`, and ordering is asserted by recording side-effects into a local `List<string>` inside custom `.Returns(...)` callbacks (see `Handle_CommitAntesDaPublicacaoDoEvento`) — this is the established way to test "A happens before B" in this codebase, not a time-based or mock-call-sequence API.

Failure-path tests simulate dependency failures with `.ThrowsAsync(new Exception(...))` and assert both that the exception propagates (`act.Should().ThrowAsync<Exception>()`) and that downstream side effects did not happen (`_publisher.DidNotReceive().Publish(...)`).

## Event-handler tests — EF Core InMemory

`ConsolidadoDiario`'s event handlers persist real aggregates, so tests wire up a real `DbContext`/repository/UnitOfWork against `UseInMemoryDatabase(Guid.NewGuid().ToString())` — a fresh, isolated database per test instance, disposed via `IDisposable`:

```csharp
public sealed class AtualizarConsolidadoEventHandlerTests : IDisposable
{
    private readonly ConsolidadoDbContext _dbContext;

    public AtualizarConsolidadoEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ConsolidadoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ConsolidadoDbContext(options);
        // repository, unit of work and handler(s) built from _dbContext
    }

    public void Dispose() => _dbContext.Dispose();
}
```

Logger dependencies are satisfied with `NullLogger<T>.Instance` (`Microsoft.Extensions.Logging.Abstractions`), not NSubstitute — loggers are not asserted on in these tests.

## Coverage

No coverage tool, threshold, or CI gate is configured (no `coverlet` reference, no `.runsettings`, no pipeline file). The README documents `dotnet test fluxocaixa.sln --collect:"XPlat Code Coverage"` as an optional manual command, but no coverage numbers are enforced. Treat any future coverage target as a decision to make with the user, not an existing rule.

### Current test counts (per README)

| Suite | Tests | Covers |
|---|---|---|
| `Lancamentos.UnitTests/Domain` | 11 | `Lancamento`, `Dinheiro`: creation, validation, events, rounding |
| `Lancamentos.UnitTests/Application` | 11 | `CriarLancamento`/`RemoverLancamento` handlers: repo/commit/publish ordering, failures |
| `ConsolidadoDiario.UnitTests` | 10 | Event handlers: creation, accumulation, reversal, distinct dates, negative balance guard, silent-failure isolation |

## Running Tests

```bash
# Run all tests in the solution
dotnet test fluxocaixa.sln

# Run a single project
dotnet test tests/Lancamentos.UnitTests/Lancamentos.UnitTests.csproj

# With coverage (coverlet.collector is not referenced — this only works if added)
dotnet test fluxocaixa.sln --collect:"XPlat Code Coverage"
```

## CI/CD Integration

None exists. There is no `.github/workflows` directory or any other pipeline definition in this repository — `dotnet test` must be run manually before considering work done (see [dod.md](dod.md)).
