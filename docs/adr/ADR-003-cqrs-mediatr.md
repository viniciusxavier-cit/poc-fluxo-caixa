# ADR-003 — Implementar CQRS com MediatR

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-001 (Clean Architecture), ADR-004 (Domain Events)

---

## Contexto

A camada Application precisa de um mecanismo para orquestrar casos de uso sem criar dependências diretas entre a API e os handlers de negócio. Os casos de uso do sistema se dividem claramente em dois grupos:

- **Commands** — intenções de mudar estado: `CriarLancamento`, `RemoverLancamento`
- **Queries** — intenções de ler estado: `GetLancamentoPorData`, `GetLancamentoPorId`, `GetConsolidadoPorData`

Commands e Queries têm características distintas:
- Commands precisam de validação, persistência e publicação de eventos
- Queries precisam apenas de leitura eficiente, sem side effects

O sistema também precisa publicar Domain Events após persistência, sem que o Command Handler conheça os consumidores.

---

## Decisão

Implementar **CQRS (Command Query Responsibility Segregation)** usando **MediatR** como dispatcher in-process.

### Separação Command / Query

```
Application/
├── Commands/
│   ├── CriarLancamento/
│   │   ├── CriarLancamentoCommand.cs       ← IRequest<LancamentoDto>
│   │   └── CriarLancamentoCommandHandler.cs ← IRequestHandler<...>
│   └── RemoverLancamento/
│       ├── RemoverLancamentoCommand.cs      ← IRequest
│       └── RemoverLancamentoCommandHandler.cs
└── Queries/
    ├── GetLancamentoPorData/
    │   ├── GetLancamentoPorDataQuery.cs     ← IRequest<IReadOnlyList<LancamentoDto>>
    │   └── GetLancamentoPorDataQueryHandler.cs
    └── GetLancamentoPorId/
        ├── GetLancamentoPorIdQuery.cs       ← IRequest<LancamentoDto?>
        └── GetLancamentoPorIdQueryHandler.cs
```

### Fluxo de um Command

```
API Endpoint
  └─► mediator.Send(CriarLancamentoCommand)
        └─► CriarLancamentoCommandHandler.Handle()
              ├── Lancamento.Criar()           ← domínio valida
              ├── repository.AddAsync()
              ├── unitOfWork.CommitAsync()     ← persistência garantida
              └── publisher.Publish(evento)    ← side effects desacoplados
```

### Papel do IPublisher vs IMediator

- `IMediator.Send()` — para Commands e Queries (1 handler, retorna resultado)
- `IPublisher.Publish()` — para Domain Events (0..N handlers, sem retorno)

Essa distinção semântica é intencional: um evento pode ter múltiplos consumidores futuros sem alterar o command handler.

### Registro dos handlers

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CriarLancamentoCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AtualizarConsolidadoEventHandler).Assembly);
});
```

---

## Alternativas Consideradas

### Service Layer tradicional (ILancamentoService)

Uma interface de serviço com métodos como `CriarAsync`, `RemoverAsync`, `GetPorDataAsync`.

Rejeitado porque:
- Viola SRP — um service de lançamentos acaba com 5+ métodos não relacionados
- Escalar o modelo (ex: adicionar pipeline behaviors para logging, validação) exige modificar o service
- Não separa semanticamente leituras de escritas

### Injetar handlers diretamente nos endpoints

```csharp
app.MapPost("/lancamentos", (CriarLancamentoCommandHandler handler, ...) => ...)
```

Rejeitado porque:
- Viola OCP — cada novo handler requer mudança na injeção do endpoint
- Impossibilita adicionar behaviors transversais (logging, validação, retry) de forma uniforme
- Cria acoplamento direto entre API e camada Application

### CQRS com bancos separados de leitura e escrita

Read models projetados em tabelas desnormalizadas para leitura.

Não adotado neste momento porque:
- Adiciona complexidade de sincronização entre write e read models
- O volume de dados não justifica a otimização neste estágio
- Pode ser evoluído a partir da estrutura atual sem breaking change (o event handler já projeta dados)

---

## Consequências

**Positivas:**
- SRP: cada handler tem exatamente uma responsabilidade
- OCP: novos casos de uso são adicionados criando novos arquivos, sem modificar existentes
- DIP: os endpoints dependem de `IMediator`, não dos handlers concretos
- Behaviors transversais (logging, validação com FluentValidation, retry) podem ser adicionados via `IPipelineBehavior<TRequest, TResponse>` sem tocar nos handlers
- Testabilidade: cada handler é testável isoladamente com mocks de repositório

**Negativas:**
- Mais arquivos para casos de uso simples
- `IPublisher.Publish()` é fire-and-forget in-process: se o processo cair entre a persistência e a publicação, o evento é perdido (mitigado em produção pelo Outbox Pattern — ver ADR-004)
- Desenvolvedor precisa saber qual `IRequest` usar para cada operação

---

## Referências

- [CQRS (Martin Fowler)](https://martinfowler.com/bliki/CQRS.html)
- [MediatR — Jimmy Bogard](https://github.com/jbogard/MediatR)
- Greg Young — *CQRS Documents* (2010)
