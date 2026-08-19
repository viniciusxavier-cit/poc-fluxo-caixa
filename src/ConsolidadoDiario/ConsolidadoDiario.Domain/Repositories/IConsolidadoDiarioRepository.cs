using ConsolidadoDiario.Domain.Entities;

namespace ConsolidadoDiario.Domain.Repositories;

public interface IConsolidadoDiarioRepository
{
    Task<ConsolidadoDiario?> GetByDataAsync(DateOnly data, CancellationToken cancellationToken = default);
    Task AddAsync(ConsolidadoDiario consolidado, CancellationToken cancellationToken = default);
    void Update(ConsolidadoDiario consolidado);
}
