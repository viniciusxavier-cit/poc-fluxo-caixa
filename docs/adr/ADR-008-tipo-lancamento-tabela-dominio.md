# ADR-008 — TipoLancamento como tabela de domínio no banco de dados

- **Status:** Aceito
- **Data:** 2025-06
- **Substitui parcialmente:** ADR-005 (seção de mapeamento de TipoLancamento)
- **Contexto relacionado:** ADR-002 (DDD), ADR-005 (EF Core)

---

## Contexto

O `TipoLancamento` (Crédito / Débito) é um conceito do domínio com significado de negócio. A implementação inicial o tratava como um enum C# armazenado como string na tabela `Lancamentos`.

Problemas dessa abordagem:

1. **Sem integridade referencial** — o banco aceita qualquer string; um bug pode inserir `"credito"` (minúsculo) sem erro
2. **Difícil extensibilidade** — adicionar um novo tipo (ex: `Estorno`, `Transferencia`) exige recompilar e fazer deploy
3. **Sem metadados** — não há onde armazenar descrição, ícone, regras de exibição para o tipo
4. **Não comunica intenção de domínio** — uma tabela de domínio torna explícito que tipos de lançamento são um conceito gerenciado pelo negócio

---

## Decisão

Criar a tabela `TiposLancamento` como **tabela de domínio** com seed data, mantendo o enum C# no domínio para type safety em código.

### Estrutura da tabela

```sql
CREATE TABLE TiposLancamento (
    Id          INTEGER NOT NULL PRIMARY KEY,
    Nome        TEXT    NOT NULL UNIQUE,
    Descricao   TEXT    NOT NULL
);

INSERT INTO TiposLancamento VALUES (1, 'Credito', 'Entrada de valor no caixa');
INSERT INTO TiposLancamento VALUES (2, 'Debito',  'Saída de valor do caixa');
```

### Modelo de dados resultante

```
TIPOS_LANCAMENTO (tabela de domínio)
  Id (PK int)  │  Nome (UK)  │  Descricao
  ─────────────┼─────────────┼─────────────────────────────
       1        │  Credito    │  Entrada de valor no caixa
       2        │  Debito     │  Saída de valor do caixa

LANCAMENTOS
  Id  │  TipoLancamentoId (FK → TiposLancamento.Id)  │  Valor  │  ...
```

### Mapeamento EF Core

O enum `TipoLancamento` é convertido para `int` via `HasConversion<int>()`, que mapeia diretamente para os IDs da tabela de domínio:

```csharp
// LancamentoConfiguration.cs
builder.Property(l => l.Tipo)
    .HasConversion<int>()
    .HasColumnName("TipoLancamentoId")
    .IsRequired();

builder.HasOne<TipoLancamentoEntity>()
    .WithMany()
    .HasForeignKey("TipoLancamentoId")
    .OnDelete(DeleteBehavior.Restrict);
```

O `DeleteBehavior.Restrict` garante que não é possível remover um tipo que possui lançamentos associados.

### Seed data via `HasData`

```csharp
// TipoLancamentoConfiguration.cs
builder.HasData(
    new { Id = 1, Nome = "Credito", Descricao = "Entrada de valor no caixa" },
    new { Id = 2, Nome = "Debito",  Descricao = "Saída de valor do caixa"  }
);
```

O seed é aplicado automaticamente no `Database.EnsureCreated()`.

### Por que manter o enum no domínio?

O enum `TipoLancamento` permanece no código C# por três razões:

1. **Type safety** — `Lancamento.Criar(TipoLancamento.Credito, ...)` não aceita valores inválidos em compile time
2. **Expressividade** — `if (tipo == TipoLancamento.Debito)` é mais legível que `if (tipoId == 2)`
3. **Sem overhead de navegação** — não precisamos carregar `TiposLancamento` do banco para usar o tipo em queries

A `TipoLancamentoEntity` existe apenas para que o EF Core estabeleça a FK — não é usada diretamente nos handlers.

---

## Alternativas Consideradas

### Enum armazenado como string

```csharp
builder.Property(l => l.Tipo).HasConversion<string>();
```

Rejeitado porque:
- Sem FK — banco aceita strings inválidas
- Difícil de agregar por tipo em queries SQL
- Não documenta o conceito de tipo como entidade de domínio

### Enum armazenado como int sem tabela de lookup

```csharp
builder.Property(l => l.Tipo).HasConversion<int>();
// sem HasOne / sem TiposLancamento
```

Melhor que string, mas rejeitado porque:
- Sem integridade referencial (FK)
- `SELECT TipoLancamentoId FROM Lancamentos` retorna `1` ou `2` sem contexto
- Dificulta queries analíticas e relatórios fora do sistema

### Smartenum / Enumeration class (DDD)

Substituir o `enum` por uma classe de domínio com comportamento:

```csharp
public abstract class TipoLancamento : Enumeration
{
    public static readonly TipoLancamento Credito = new CreditoTipo();
    public static readonly TipoLancamento Debito  = new DebitoTipo();
}
```

Considerado para cenários onde cada tipo tem comportamento diferente (ex: cálculo de taxa). Não adotado neste momento porque:
- Crédito e Débito não diferem em comportamento — apenas em direção do fluxo
- Adiciona complexidade sem benefício imediato
- A tabela de domínio + enum resolve o problema de integridade sem overhead

---

## Consequências

**Positivas:**
- Integridade referencial garantida pelo banco — FK impede valores inválidos
- Tabela `TiposLancamento` é legível em queries SQL diretas
- Extensível: adicionar `Estorno` = inserir uma linha + novo valor no enum (sem recompilar a infra)
- `DeleteBehavior.Restrict` previne remoção acidental de tipos em uso

**Negativas:**
- JOIN adicional em queries que precisam do nome do tipo (mitigado por `AsNoTracking` e projeção via DTO)
- O enum C# e a tabela precisam estar sincronizados — se um novo valor for inserido no banco sem atualizar o enum, o `HasConversion<int>()` lançará exceção
- `EnsureCreated()` aplica o seed; com migrations seria necessário um `migrationBuilder.InsertData()`

---

## Referências

- [Enumeration Classes (Microsoft — microservices eShop)](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/enumeration-classes-over-enum-types)
- [EF Core — HasConversion](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [EF Core — HasData (seed)](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding)
- Jimmy Bogard — *Enumeration classes in C#* (2008)
