# ADR-004 — Usar Domain Events para desacoplar Lançamentos de Consolidado Diário

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-002 (DDD), ADR-003 (CQRS/MediatR)

---

## Contexto

**Requisito crítico:** o serviço de lançamentos **não pode ficar indisponível** se o serviço de consolidado diário cair.

Isso impõe uma restrição arquitetural forte: a operação de registrar um lançamento deve ser completada com sucesso **independentemente** do estado do serviço de consolidado. Os dois bounded contexts não podem ter uma transação distribuída sincronizada.

O fluxo desejado é:

1. Cliente chama `POST /lancamentos`
2. Lançamento é validado e persistido no banco de Lançamentos ✅
3. API responde `201 Created` ao cliente ✅
4. O consolidado é atualizado de forma eventual, sem bloquear o passo 2

Se o passo 4 falhar, o lançamento já foi salvo e a API já respondeu. O consolidado pode ser recalculado quando o serviço estiver disponível novamente.

---

## Decisão

Usar **Domain Events** publicados via `IPublisher` do MediatR (in-process), com as seguintes garantias:

### Contrato do evento

```csharp
public sealed record LancamentoCriadoEvent(
    Guid LancamentoId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly Data,
    DateTime OcorridoEm) : IDomainEvent;
```

### Sequência de operações no handler

```csharp
// CriarLancamentoCommandHandler.Handle()
var lancamento = Lancamento.Criar(request.Tipo, request.Valor, request.Descricao, request.Data);

await _repository.AddAsync(lancamento, cancellationToken);
await _unitOfWork.CommitAsync(cancellationToken);  // ← lançamento persistido ANTES do evento

foreach (var @event in lancamento.DomainEvents)
    await _publisher.Publish(@event, cancellationToken);  // ← evento publicado DEPOIS

lancamento.ClearDomainEvents();
```

A ordem `CommitAsync → Publish` é a garantia de resiliência: se o event handler falhar, o `CommitAsync` já foi executado.

### Isolamento de falha no event handler

```csharp
// AtualizarConsolidadoEventHandler.Handle()
public async Task Handle(LancamentoCriadoEvent notification, CancellationToken cancellationToken)
{
    try
    {
        // atualiza consolidado...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Falha ao atualizar consolidado para data {Data}. " +
            "O lançamento {LancamentoId} foi persistido.", ...);
        // falha silenciosa — não propaga para o command handler
    }
}
```

O `try/catch` garante que uma falha no consolidado não causa rollback no lançamento.

### Diagrama de resiliência

```
CENÁRIO 1 — Fluxo normal
─────────────────────────
Lancamento persistido ✅ → Evento publicado ✅ → Consolidado atualizado ✅
Cliente recebe 201 ✅

CENÁRIO 2 — Consolidado com falha
──────────────────────────────────
Lancamento persistido ✅ → Evento publicado ✅ → Consolidado falha ⚠️
Cliente recebe 201 ✅ (lançamento salvo)
Log de erro registrado

CENÁRIO 3 — Falha antes do commit
──────────────────────────────────
Lancamento NÃO persistido ❌ → Evento NÃO publicado ❌
Cliente recebe 500 (correto — nada foi salvo)
```

---

## Limitação atual e caminho para produção

**Problema:** Com MediatR in-process, se o processo cair **após** `CommitAsync` e **antes** de `Publish`, o evento é perdido. O consolidado ficará desatualizado permanentemente até intervenção manual.

**Solução para produção — Outbox Pattern:**

```
┌─────────────────────────────────┐
│  Mesma transação do EF Core     │
│  ┌────────────┐  ┌────────────┐ │
│  │ Lancamento │  │  Outbox    │ │
│  │ persistido │  │  Message   │ │
│  └────────────┘  └────────────┘ │
└─────────────────────────────────┘
        │
        ▼
  Background Worker
  (polling Outbox)
        │
        ▼
  RabbitMQ / Azure Service Bus
        │
        ▼
  ConsolidadoDiario.API
  (consumer independente)
```

A abstração `IMessageBus` no SharedKernel pode substituir `IPublisher` sem alterar o Domain ou a Application layer.

---

## Alternativas Consideradas

### Chamada HTTP direta do serviço de Lançamentos ao Consolidado

```csharp
// No handler: chamar HTTP do serviço de consolidado
await _consolidadoHttpClient.AtualizarAsync(data, tipo, valor);
```

Rejeitado porque:
- Viola diretamente o requisito de resiliência — se o consolidado estiver fora do ar, o lançamento falha
- Cria acoplamento temporal entre os dois serviços
- Timeout do HTTP pode tornar o endpoint de lançamentos lento

### Transação distribuída (2PC / Saga)

Coordenar os dois bancos em uma transação distribuída.

Rejeitado porque:
- Two-Phase Commit é complexo, frágil e tem problemas de performance em SQLite
- Viola o princípio de bounded contexts independentes
- Saga orquestrada é overhead excessivo para este domínio

### Cálculo do consolidado on-demand (sem banco separado)

Calcular o saldo no momento da consulta via `SUM()` sobre a tabela de lançamentos.

Rejeitado porque:
- O serviço de consolidado dependeria do banco de lançamentos — violaria o requisito de independência
- Performance degradaria com volume crescente de lançamentos

---

## Consequências

**Positivas:**
- Serviço de lançamentos nunca é bloqueado por falha no consolidado ✅
- Bounded contexts mantêm autonomia de dados ✅
- A abstração `IDomainEvent` + `IPublisher` permite trocar o mecanismo de entrega sem alterar Domain ou Application ✅
- Falhas no consolidado são registradas em log e identificáveis para reprocessamento ✅

**Negativas:**
- Consistência eventual: há uma janela (milissegundos) em que o consolidado pode não refletir o lançamento mais recente
- Risco de perda de evento entre `CommitAsync` e `Publish` (mitigado pelo Outbox Pattern em produção)
- O consolidado pode divergir do saldo real em caso de falhas repetidas sem monitoramento

---

## Referências

- [Domain Events: Design and Implementation (Microsoft)](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Outbox Pattern (Kamil Grzybek)](https://www.kamilgrzybek.com/design/the-outbox-pattern/)
- [Transactional Outbox (microservices.io)](https://microservices.io/patterns/data/transactional-outbox.html)
- Udi Dahan — *Clarified CQRS* (2009)
