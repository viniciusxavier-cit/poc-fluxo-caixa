using Lancamentos.Domain.Entities;

namespace Lancamentos.Domain.Repositories;

public interface ILancamentoRepository
{
    Task<Lancamento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lancamento>> GetByDataAsync(DateOnly data, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lancamento>> GetByIntervaloAsync(DateOnly dataInicio, DateOnly dataFim, CancellationToken cancellationToken = default);
    Task AddAsync(Lancamento lancamento, CancellationToken cancellationToken = default);
    void Remove(Lancamento lancamento);
}
