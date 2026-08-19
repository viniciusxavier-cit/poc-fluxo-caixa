using Lancamentos.Domain.Entities;
using Lancamentos.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lancamentos.Infrastructure.Persistence.Configurations;

public sealed class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .ValueGeneratedNever();

        builder.Property(l => l.Tipo)
            .HasConversion<int>()
            .HasColumnName("TipoLancamentoId")
            .IsRequired();

        builder.HasOne<TipoLancamentoEntity>()
            .WithMany()
            .HasForeignKey("TipoLancamentoId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(l => l.Valor, v =>
        {
            v.Property(d => d.Valor)
                .HasColumnName("Valor")
                .HasPrecision(18, 2)
                .IsRequired();
        });

        builder.Property(l => l.Descricao)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(l => l.Data)
            .IsRequired();

        builder.Property(l => l.CriadoEm)
            .IsRequired();

        builder.Ignore(l => l.DomainEvents);
    }
}
