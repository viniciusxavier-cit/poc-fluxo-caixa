using ConsolidadoDiario.Domain.Repositories;
using ConsolidadoDiario.Infrastructure.Persistence;

namespace ConsolidadoDiario.Infrastructure;

public sealed class ConsolidadoUnitOfWork : IConsolidadoUnitOfWork
{
    private readonly ConsolidadoDbContext _context;

    public ConsolidadoUnitOfWork(ConsolidadoDbContext context) => _context = context;

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
