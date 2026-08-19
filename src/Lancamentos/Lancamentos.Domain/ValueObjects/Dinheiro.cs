using SharedKernel;

namespace Lancamentos.Domain.ValueObjects;

public sealed class Dinheiro : ValueObject
{
    public decimal Valor { get; }

    private Dinheiro(decimal valor) => Valor = valor;

    public static Dinheiro Criar(decimal valor)
    {
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(valor));

        return new Dinheiro(Math.Round(valor, 2));
    }

    public static implicit operator decimal(Dinheiro dinheiro) => dinheiro.Valor;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor.ToString("C2");
}
