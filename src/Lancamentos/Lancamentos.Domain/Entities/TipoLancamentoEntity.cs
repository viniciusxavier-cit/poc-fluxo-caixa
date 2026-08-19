namespace Lancamentos.Domain.Entities;

public sealed class TipoLancamentoEntity
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;

    private TipoLancamentoEntity() { }
}
