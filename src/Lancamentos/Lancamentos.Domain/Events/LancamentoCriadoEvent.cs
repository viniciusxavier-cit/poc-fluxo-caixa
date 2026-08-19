using SharedKernel;

namespace Lancamentos.Domain.Events;

public sealed record LancamentoCriadoEvent(
    Guid LancamentoId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly Data,
    DateTime OcorridoEm) : IDomainEvent;
