using Lancamentos.Domain.Events;
using SharedKernel;

namespace Lancamentos.Domain.Entities;

public sealed class Lancamento : AggregateRoot<Guid>
{
    public TipoLancamento Tipo { get; private set; }
    public Dinheiro Valor { get; private set; } = null!;
    public string Descricao { get; private set; } = string.Empty;
    public DateOnly Data { get; private set; }
    public DateTime CriadoEm { get; private set; }

    private Lancamento() { }

    public static Lancamento Criar(TipoLancamento tipo, decimal valor, string descricao, DateOnly data)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição é obrigatória.", nameof(descricao));

        var lancamento = new Lancamento
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Valor = Dinheiro.Criar(valor),
            Descricao = descricao.Trim(),
            Data = data,
            CriadoEm = DateTime.UtcNow
        };

        lancamento.AddDomainEvent(new LancamentoCriadoEvent(
            lancamento.Id,
            lancamento.Tipo,
            lancamento.Valor.Valor,
            lancamento.Data,
            lancamento.CriadoEm));

        return lancamento;
    }

    public void Remover()
    {
        AddDomainEvent(new LancamentoRemovidoEvent(
            Id, Tipo, Valor.Valor, Data, DateTime.UtcNow));
    }
}
