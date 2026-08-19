using Lancamentos.Domain.ValueObjects;
using SharedKernel;

namespace ConsolidadoDiario.Domain.Entities;

public sealed class ConsolidadoDiario : AggregateRoot<Guid>
{
    public DateOnly Data { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal Saldo => TotalCreditos - TotalDebitos;
    public DateTime AtualizadoEm { get; private set; }

    private ConsolidadoDiario() { }

    public static ConsolidadoDiario Criar(DateOnly data)
    {
        return new ConsolidadoDiario
        {
            Id = Guid.NewGuid(),
            Data = data,
            TotalCreditos = 0,
            TotalDebitos = 0,
            AtualizadoEm = DateTime.UtcNow
        };
    }

    public void AplicarLancamento(TipoLancamento tipo, decimal valor)
    {
        if (valor <= 0)
            throw new ArgumentException("Valor deve ser positivo.", nameof(valor));

        if (tipo == TipoLancamento.Credito)
            TotalCreditos += valor;
        else
            TotalDebitos += valor;

        AtualizadoEm = DateTime.UtcNow;
    }

    public void EstornarLancamento(TipoLancamento tipo, decimal valor)
    {
        if (tipo == TipoLancamento.Credito)
            TotalCreditos = Math.Max(0, TotalCreditos - valor);
        else
            TotalDebitos = Math.Max(0, TotalDebitos - valor);

        AtualizadoEm = DateTime.UtcNow;
    }
}
