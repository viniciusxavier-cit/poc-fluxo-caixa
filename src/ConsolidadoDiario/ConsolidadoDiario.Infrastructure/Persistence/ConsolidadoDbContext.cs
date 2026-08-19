using Microsoft.EntityFrameworkCore;

namespace ConsolidadoDiario.Infrastructure.Persistence;

public sealed class ConsolidadoDbContext : DbContext
{
    public ConsolidadoDbContext(DbContextOptions<ConsolidadoDbContext> options) : base(options) { }

    public DbSet<ConsolidadoDiario.Domain.Entities.ConsolidadoDiario> Consolidados =>
        Set<ConsolidadoDiario.Domain.Entities.ConsolidadoDiario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsolidadoDbContext).Assembly);
    }
}
