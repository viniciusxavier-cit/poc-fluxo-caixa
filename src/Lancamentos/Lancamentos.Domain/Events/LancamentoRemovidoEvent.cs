using Lancamentos.Domain.ValueObjects;
using SharedKernel;

namespace Lancamentos.Domain.Events;

public sealed record LancamentoRemovidoEvent(
    Guid LancamentoId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly Data,
    DateTime OcorridoEm) : IDomainEvent;
