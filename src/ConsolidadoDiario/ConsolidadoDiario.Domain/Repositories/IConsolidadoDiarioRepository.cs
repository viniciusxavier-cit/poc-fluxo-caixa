using ConsolidadoDiarioEntity = ConsolidadoDiario.Domain.Entities.ConsolidadoDiario;

namespace ConsolidadoDiario.Domain.Repositories;

public interface IConsolidadoDiarioRepository
{
    Task<ConsolidadoDiarioEntity?> GetByDataAsync(DateOnly data, CancellationToken cancellationToken = default);
    Task AddAsync(ConsolidadoDiarioEntity consolidado, CancellationToken cancellationToken = default);
    void Update(ConsolidadoDiarioEntity consolidado);
}
