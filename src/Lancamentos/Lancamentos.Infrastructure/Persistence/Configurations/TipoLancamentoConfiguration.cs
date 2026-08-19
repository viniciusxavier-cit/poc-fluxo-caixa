using Lancamentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lancamentos.Infrastructure.Persistence.Configurations;

public sealed class TipoLancamentoConfiguration : IEntityTypeConfiguration<TipoLancamentoEntity>
{
    public void Configure(EntityTypeBuilder<TipoLancamentoEntity> builder)
    {
        builder.ToTable("TiposLancamento");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Nome)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Descricao)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(t => t.Nome).IsUnique();

        builder.HasData(
            new { Id = 1, Nome = "Credito", Descricao = "Entrada de valor no caixa" },
            new { Id = 2, Nome = "Debito",  Descricao = "Saída de valor do caixa" }
        );
    }
}
