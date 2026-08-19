using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConsolidadoDiario.Infrastructure.Persistence.Configurations;

public sealed class ConsolidadoDiarioConfiguration
    : IEntityTypeConfiguration<ConsolidadoDiario.Domain.Entities.ConsolidadoDiario>
{
    public void Configure(EntityTypeBuilder<ConsolidadoDiario.Domain.Entities.ConsolidadoDiario> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.Data)
            .IsRequired();

        builder.HasIndex(c => c.Data)
            .IsUnique();

        builder.Property(c => c.TotalCreditos)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.TotalDebitos)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.AtualizadoEm)
            .IsRequired();

        builder.Ignore(c => c.Saldo);
        builder.Ignore(c => c.DomainEvents);
    }
}
