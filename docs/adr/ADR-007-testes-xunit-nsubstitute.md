# ADR-007 — Usar xUnit + FluentAssertions + NSubstitute para testes

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-001 (Clean Architecture), ADR-002 (DDD)

---

## Contexto

O desafio exige testes. Precisamos de uma estratégia que:

1. Teste as regras de negócio do domínio **sem banco de dados**
2. Teste os handlers da camada Application **de forma isolada**
3. Seja expressiva — asserções legíveis como documentação
4. Siga LSP do SOLID — `ILancamentoRepository` deve ser substituível por um mock
5. Permita testar o event handler do consolidado com banco em memória (comportamento mais próximo do real)

---

## Decisão

Usar o stack de testes:

| Biblioteca | Versão | Papel |
|---|---|---|
| **xUnit** | 2.9 | Framework de testes |
| **FluentAssertions** | 6.12 | Asserções legíveis |
| **NSubstitute** | 5.3 | Mocking de interfaces |
| **EF Core InMemory** | 8.0 | DB em memória para testes de handler |

### Estrutura de testes

```
tests/
├── Lancamentos.UnitTests/
│   ├── Domain/
│   │   └── LancamentoTests.cs         ← Sem mocks. Testa Aggregate e Value Objects.
│   └── Application/
│       └── CriarLancamentoCommandHandlerTests.cs ← NSubstitute para IRepository e IUnitOfWork
│
└── ConsolidadoDiario.UnitTests/
    └── Application/
        └── AtualizarConsolidadoEventHandlerTests.cs ← EF InMemory para comportamento real
```

### Testes de Domínio — sem mocks

As regras do domínio (Value Objects, invariantes do Aggregate) são testadas diretamente, sem nenhuma dependência externa:

```csharp
[Fact]
public void Criar_ComValorInvalido_DeveLancarExcecao()
{
    var act = () => Lancamento.Criar(TipoLancamento.Debito, -10m, "Teste", DateOnly.FromDateTime(DateTime.Today));

    act.Should().Throw<ArgumentException>().WithMessage("*maior que zero*");
}
```

Isso é possível porque a Clean Architecture mantém o Domain sem dependências externas.

### Testes de Application — NSubstitute

Handlers são testados com dependências mockadas:

```csharp
private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
private readonly IPublisher _publisher = Substitute.For<IPublisher>();

[Fact]
public async Task Handle_DeveFazerCommit()
{
    var command = new CriarLancamentoCommand(TipoLancamento.Credito, 200m, "TED", DateOnly.FromDateTime(DateTime.Today));

    await _handler.Handle(command, CancellationToken.None);

    await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
}
```

NSubstitute verifica:
- `.Received(1)` — método foi chamado exatamente uma vez
- `Arg.Any<T>()` — argumento de qualquer valor do tipo T

### Testes de Event Handler — EF Core InMemory

O `AtualizarConsolidadoEventHandler` lida com persistência real. InMemory é preferível a NSubstitute aqui porque:
- Testa o comportamento de upsert (criar ou atualizar)
- Verifica acúmulo correto de créditos e débitos
- Detecta erros de mapeamento EF Core antecipadamente

```csharp
public sealed class AtualizarConsolidadoEventHandlerTests : IDisposable
{
    private readonly ConsolidadoDbContext _dbContext;

    public AtualizarConsolidadoEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ConsolidadoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())  // banco único por teste
            .Options;
        // ...
    }

    [Fact]
    public async Task Handle_PrimeiroLancamento_DeveCriarConsolidado()
    {
        var @event = new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 500m, data, DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.TotalCreditos.Should().Be(500m);
        consolidado.Saldo.Should().Be(500m);
    }
}
```

`Guid.NewGuid()` como nome do banco garante isolamento entre testes — cada teste tem seu próprio banco em memória.

### Nomenclatura dos testes

Padrão: `MetodoOuCenario_CondicaoEntrada_ResultadoEsperado`

```
Criar_ComDadosValidos_DeveCriarLancamento
Criar_ComValorInvalido_DeveLancarExcecao
Handle_DeveAdicionarLancamentoNoRepositorio
Handle_PrimeiroLancamento_DeveCriarConsolidado
```

---

## Alternativas Consideradas

### NUnit

Alternativa popular ao xUnit.

Não adotado porque:
- xUnit é o padrão de facto no ecossistema ASP.NET Core e nos templates da Microsoft
- `[Fact]` e `[Theory]` são semânticos e não requerem setup/teardown por classe

### Moq

O mock framework mais usado no .NET.

NSubstitute adotado em preferência porque:
- Sintaxe mais limpa: `_repo.Received(1).AddAsync(...)` vs `_mock.Verify(r => r.AddAsync(...), Times.Once)`
- Menos verboso para configurar retornos

### Testes de integração com SQLite real

Testar com banco SQLite real em vez de InMemory.

Não adotado como padrão principal porque:
- Cria dependência de arquivo em disco nos testes
- Dificulta paralelização dos testes
- InMemory é suficiente para verificar a lógica dos handlers

Para produção, recomenda-se adicionar uma suite de testes de integração separada com SQLite (ou banco de staging).

---

## Consequências

**Positivas:**
- Testes de domínio são rápidos (< 1ms cada) — sem I/O
- LSP demonstrado: `LancamentoRepository` e `Substitute.For<ILancamentoRepository>()` são intercambiáveis
- FluentAssertions tornam as falhas descritivas: `Expected 500, but found 0`
- DIP validado: handlers não precisam de banco real para ser testados

**Negativas:**
- EF Core InMemory não suporta todas as features do banco real (ex: constraints únicas reais, transações)
- Testes de Application com NSubstitute não detectam bugs de query EF Core (ex: N+1)
- Cobertura de código não garante cobertura de comportamento — testes de integração são complementares

### Cobertura atual

| Módulo | Testes | Cobre |
|---|---|---|
| `Lancamentos.Domain` | 5 | Criação, validação, Value Objects, Domain Events |
| `Lancamentos.Application` | 4 | Handler: repositório, commit, publicação de evento |
| `ConsolidadoDiario.Application` | 2 | Event handler: criação e acúmulo de consolidado |

---

## Referências

- [xUnit.net](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/)
- [NSubstitute](https://nsubstitute.github.io/)
- [Testing EF Core (Microsoft)](https://learn.microsoft.com/en-us/ef/core/testing/)
- [Unit Testing Principles, Practices, and Patterns — Vladimir Khorikov](https://www.manning.com/books/unit-testing)
