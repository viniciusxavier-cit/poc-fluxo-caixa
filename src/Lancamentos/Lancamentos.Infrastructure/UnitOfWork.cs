using Lancamentos.Infrastructure.Persistence;
using SharedKernel;

namespace Lancamentos.Infrastructure;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly LancamentosDbContext _context;

    public UnitOfWork(LancamentosDbContext context) => _context = context;

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
