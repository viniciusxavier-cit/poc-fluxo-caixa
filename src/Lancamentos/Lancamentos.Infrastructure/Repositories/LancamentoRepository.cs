using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Repositories;

public sealed class LancamentoRepository : ILancamentoRepository
{
    private readonly LancamentosDbContext _context;

    public LancamentoRepository(LancamentosDbContext context) => _context = context;

    public async Task<Lancamento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Lancamentos.FindAsync(new object[] { id }, cancellationToken);

    public async Task<IReadOnlyList<Lancamento>> GetByDataAsync(DateOnly data, CancellationToken cancellationToken = default) =>
        await _context.Lancamentos
            .Where(l => l.Data == data)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Lancamento lancamento, CancellationToken cancellationToken = default) =>
        await _context.Lancamentos.AddAsync(lancamento, cancellationToken);

    public void Remove(Lancamento lancamento) =>
        _context.Lancamentos.Remove(lancamento);
}
