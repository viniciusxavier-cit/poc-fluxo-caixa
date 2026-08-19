# ADR-005 — Usar SQLite com EF Core (banco independente por serviço)

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-002 (Bounded Contexts), ADR-001 (Clean Architecture)

---

## Contexto

O sistema precisa de persistência para dois bounded contexts independentes. Os critérios de avaliação para a escolha do banco de dados neste desafio são:

1. **Facilidade de execução local** — sem configuração de servidor externo
2. **Independência entre os serviços** — cada serviço deve ter seu próprio banco
3. **Substituibilidade** — a escolha de infraestrutura não deve afetar o domínio
4. **Demonstrar o padrão** — um banco por bounded context é uma prática de microserviços

---

## Decisão

Usar **SQLite** como banco de dados com **Entity Framework Core** como ORM, com **um arquivo `.db` por bounded context**:

| Serviço | Arquivo | Tabelas |
|---------|---------|---------|
| Lancamentos.API | `lancamentos.db` | `Lancamentos` |
| ConsolidadoDiario.API | `consolidado.db` | `Consolidados` |

### Configuração

```csharp
// Lancamentos
builder.Services.AddDbContext<LancamentosDbContext>(opt =>
    opt.UseSqlite("Data Source=lancamentos.db"));

// ConsolidadoDiario
builder.Services.AddDbContext<ConsolidadoDbContext>(opt =>
    opt.UseSqlite("Data Source=consolidado.db"));
```

### Criação automática do schema

`Database.EnsureCreated()` na inicialização — sem migrations manuais para o desafio:

```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<LancamentosDbContext>().Database.EnsureCreated();
    scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>().Database.EnsureCreated();
}
```

### Mapeamento de Value Objects com `OwnsOne`

O `Dinheiro` (Value Object) é mapeado como coluna dentro da tabela `Lancamentos`:

```csharp
builder.OwnsOne(l => l.Valor, v =>
{
    v.Property(d => d.Valor)
        .HasColumnName("Valor")
        .HasPrecision(18, 2)
        .IsRequired();
});
```

### Domain Events ignorados no mapeamento

```csharp
builder.Ignore(l => l.DomainEvents);
```

Os Domain Events são membros transitórios do Aggregate Root — não devem ser persistidos.

### Saldo calculado (não persistido)

```csharp
builder.Ignore(c => c.Saldo);
```

`Saldo = TotalCreditos - TotalDebitos` é calculado na propriedade do Aggregate — nunca armazenado no banco para evitar inconsistência.

---

## Padrão Repository e abstração

A escolha do SQLite não vaza para a camada de domínio ou aplicação graças ao Repository Pattern:

```
Domain:         ILancamentoRepository  ← interface pura
Infrastructure: LancamentoRepository  ← implementação EF Core/SQLite
```

Trocar SQLite por PostgreSQL em produção: apenas `UseSqlite` → `UseNpgsql` no `Program.cs` e ajuste de connection string. Zero alteração em Domain ou Application.

---

## Alternativas Consideradas

### PostgreSQL via Docker

Mais próximo de produção, suporte robusto a concorrência.

Não adotado para o desafio porque:
- Exige Docker em execução, adicionando fricção para avaliar o projeto
- O objetivo é demonstrar a arquitetura, não operações de banco
- A abstração com EF Core torna a migração trivial quando necessário

### SQL Server LocalDB

Disponível no Windows sem Docker.

Não adotado porque:
- Não funciona em Linux/macOS sem configuração adicional
- SQLite é multiplataforma por padrão

### InMemory (EF Core InMemory Provider)

Zero configuração, ideal para testes.

Não adotado como banco principal porque:
- Não persiste dados entre reinicializações
- Não suporta algumas features de EF Core (ex: índices únicos, transações reais)
- Mascara problemas que só aparecem com banco real (ex: tipo de dados, ordenação)

Adotado apenas nos **testes unitários** do ConsolidadoDiario onde isolamento é necessário.

### Dapper com SQL direto

Mais controle, melhor performance em queries complexas.

Não adotado porque:
- Para este domínio simples, o overhead do EF Core é negligível
- EF Core com `AsNoTracking()` tem performance adequada para as queries do sistema
- O mapeamento de Value Objects com `OwnsOne` seria manual com Dapper

---

## Consequências

**Positivas:**
- Zero configuração — `dotnet run` cria o banco automaticamente
- Banco por serviço demonstra o princípio de dados isolados por bounded context
- EF Core Configurations (`IEntityTypeConfiguration<T>`) mantêm o mapeamento explícito e testável
- `AsNoTracking()` nas queries de leitura elimina overhead de change tracking

**Negativas:**
- SQLite não é adequado para alta concorrência de escrita em produção (lock de arquivo)
- `EnsureCreated()` não suporta migrations incrementais — para produção, usar `MigrateAsync()`
- SQLite tem suporte limitado a tipos decimais (armazenado como `REAL` sem precisão nativa — mitigado pelo `HasPrecision(18,2)`)

### Caminho de produção

```
SQLite (dev/teste)
    → PostgreSQL (produção)
    → Mudança: UseSqlite → UseNpgsql no Program.cs
    → EnsureCreated → Database.MigrateAsync()
    → Adicionar migrations com: dotnet ef migrations add NomeMigration
```

---

## Referências

- [EF Core — Owned Entity Types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [EF Core — SQLite Provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [Database per Service Pattern (microservices.io)](https://microservices.io/patterns/data/database-per-service.html)
