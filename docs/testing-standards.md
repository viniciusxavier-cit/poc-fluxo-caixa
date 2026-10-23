# Testing Standards

**Last updated:** 2026-10-23

## Testing Framework

- **Unit Tests:** xUnit 2.9.2 (`xunit`, `xunit.runner.visualstudio` 2.8.2)
- **Assertions:** FluentAssertions 6.12.1 (`.Should()` fluent syntax — do not use raw `Assert.Equal`)
- **Mocking:** NSubstitute 5.3.0 (`Substitute.For<T>()` — do not introduce Moq)
- **Integration/E2E Tests:** none exist in this repo today. `Microsoft.EntityFrameworkCore.InMemory` is referenced only by `ConsolidadoDiario.UnitTests` but is not currently used to spin up a real DbContext-backed integration test — verify current usage with `grep -r InMemoryDatabase tests/` before assuming a pattern exists.

## Test Organization

Tests are organized **one project per bounded context**, mirroring `src/` structure one level in:

```
tests/
├── Lancamentos.UnitTests/
│   ├── Domain/LancamentoTests.cs                              # Aggregate + VO behavior
│   └── Application/CriarLancamentoCommandHandlerTests.cs       # Command handler behavior
└── ConsolidadoDiario.UnitTests/
    └── Application/AtualizarConsolidadoEventHandlerTests.cs    # Event handler behavior
```

There is no `tests/integration/` or `tests/e2e/` folder — all current tests are fast, isolated unit tests with dependencies substituted via NSubstitute. Each test project references only its own context's `Application` + `Infrastructure` projects (see `.csproj` `ProjectReference`s).

## Test Naming Conventions

Method naming follows `{MethodUnderTest}_{Scenario}_{ExpectedOutcome}` in Portuguese, matching the domain language used throughout the codebase:

```csharp
[Fact]
public void Criar_ComDadosValidos_DeveCriarLancamento() { ... }

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void Criar_ComValorInvalido_DeveLancarExcecao(decimal valor) { ... }

[Fact]
public async Task Handle_ComDadosValidos_DeveRetornarLancamentoDto() { ... }
```

- Test classes are `sealed`, named `{TypeUnderTest}Tests`
- `[Theory]` + `[InlineData]` is used for boundary/invalid-input variations (e.g. zero/negative values, empty/whitespace/null strings) rather than separate `[Fact]`s per case

## Test Structure (AAA Pattern)

Tests follow Arrange-Act-Assert without explicit comment markers — blank lines separate the three phases:

```csharp
[Fact]
public async Task Handle_DeveFazerCommit()
{
    var command = new CriarLancamentoCommand(
        TipoLancamento.Credito, 200m, "TED recebida", DateOnly.FromDateTime(DateTime.Today));

    await _handler.Handle(command, CancellationToken.None);

    await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
}
```

Handler test classes wire up substitutes as `readonly` fields initialized inline, with the system-under-test constructed in the test class constructor:

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
    // ...
}
```

## Mocking Strategy

- **Unit tests mock every external dependency** of the handler under test: repository, `IUnitOfWork`, `IPublisher`/`IMediator`, `ILogger<T>` — via `Substitute.For<TInterface>()`
- **Interaction verification** uses `Received(n)`: `await _repository.Received(1).AddAsync(Arg.Any<Lancamento>(), Arg.Any<CancellationToken>())`
- **Ordering assertions** (e.g. "commit happens before publish", verifying the resilience contract in [docs/api-standards.md](api-standards.md)) are done by appending to a shared `List<string>` inside `.Returns(callback)` lambdas on the substitutes, then asserting the list order
- **Domain entities and value objects are never mocked** — they are constructed via their real factory methods (`Lancamento.Criar(...)`) and asserted on directly
- No integration tests against a real SQLite file or EF Core `InMemory` provider currently exist for either context — if you add one, put it under a new `tests/{Context}.IntegrationTests/` project rather than inside the existing `UnitTests` projects

## Coverage Targets

No coverage threshold is enforced or configured (no `coverlet.runsettings`, no CI gate). Current test counts per README.md:

| Suite | Tests | Covers |
|---|---|---|
| `Lancamentos.UnitTests/Domain` | 11 | `Lancamento`, `Dinheiro`: creation, validation, events, rounding |
| `Lancamentos.UnitTests/Application` | 11 | `CriarLancamento`/`RemoverLancamento` handlers, commit→publish ordering, failure paths |
| `ConsolidadoDiario.UnitTests` | 10 | Event handlers: creation, accumulation, reversal, distinct dates, negative balance, silent-failure path |

### Coverage Commands

```bash
# Run all tests
dotnet test fluxocaixa.sln

# With coverage report (requires coverlet, not currently wired into CI)
dotnet test fluxocaixa.sln --collect:"XPlat Code Coverage"
```

## CI/CD Integration

**No CI pipeline exists in this repository** — there is no `.github/workflows/`, GitLab CI config, or Azure Pipelines file. Tests must be run locally via `dotnet test` before submitting any change (see [docs/dod.md](dod.md)).

## Test Data Management

- No fixtures, factories, or database seeding exist — each test constructs its own minimal valid data inline (e.g. `DateOnly.FromDateTime(DateTime.Today)`, literal `decimal` amounts)
- No teardown/cleanup logic is needed since no test touches a real database or shared state

## Best Practices (observed in this codebase — follow them)

- ✅ One behavior asserted per test method; multiple `[InlineData]` cases share one assertion shape
- ✅ Test names and messages are in Portuguese, matching domain language (`"A descrição é obrigatória."`)
- ✅ Handler tests assert both the returned value AND the side effects (repository call, commit, publish) as separate `[Fact]`s rather than one giant test
- ✅ Silent-failure contracts (e.g. `AtualizarConsolidadoEventHandler` swallowing exceptions) are explicitly tested, not just implemented
- ❌ Don't mock `Lancamento`/`ConsolidadoDiario` aggregates — always build them through their factory methods
- ❌ Don't add a coverage gate or CI config as a side effect of an unrelated task — that's a deliberate infra decision for the user to make
