using ConsolidadoDiario.Domain.Repositories;
using ConsolidadoDiario.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsolidadoDiario.Infrastructure.Repositories;

public sealed class ConsolidadoDiarioRepository : IConsolidadoDiarioRepository
{
    private readonly ConsolidadoDbContext _context;

    public ConsolidadoDiarioRepository(ConsolidadoDbContext context) => _context = context;

    public async Task<ConsolidadoDiario.Domain.Entities.ConsolidadoDiario?> GetByDataAsync(
        DateOnly data, CancellationToken cancellationToken = default) =>
        await _context.Consolidados
            .FirstOrDefaultAsync(c => c.Data == data, cancellationToken);

    public async Task AddAsync(
        ConsolidadoDiario.Domain.Entities.ConsolidadoDiario consolidado,
        CancellationToken cancellationToken = default) =>
        await _context.Consolidados.AddAsync(consolidado, cancellationToken);

    public void Update(ConsolidadoDiario.Domain.Entities.ConsolidadoDiario consolidado) =>
        _context.Consolidados.Update(consolidado);
}
