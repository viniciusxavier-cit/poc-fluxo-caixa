using FluentAssertions;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Events;
using Lancamentos.Domain.ValueObjects;

namespace Lancamentos.UnitTests.Domain;

public sealed class LancamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_DeveCriarLancamento()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, "Venda balcão", data);

        lancamento.Id.Should().NotBeEmpty();
        lancamento.Tipo.Should().Be(TipoLancamento.Credito);
        lancamento.Valor.Valor.Should().Be(100m);
        lancamento.Descricao.Should().Be("Venda balcão");
        lancamento.Data.Should().Be(data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Criar_ComValorInvalido_DeveLancarExcecao(decimal valor)
    {
        var act = () => Lancamento.Criar(TipoLancamento.Debito, valor, "Pagamento", DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentException>().WithMessage("*maior que zero*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComDescricaoVazia_DeveLancarExcecao(string? descricao)
    {
        var act = () => Lancamento.Criar(TipoLancamento.Credito, 50m, descricao!, DateOnly.FromDateTime(DateTime.Today));

        act.Should().Throw<ArgumentException>().WithMessage("*descrição*");
    }

    [Fact]
    public void Criar_DevePublicarLancamentoCriadoEvent()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 250m, "PIX recebido", DateOnly.FromDateTime(DateTime.Today));

        lancamento.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LancamentoCriadoEvent>();
    }

    [Fact]
    public void Criar_EventoDeveConterDadosCorretos()
    {
        var data = new DateOnly(2025, 6, 1);

        var lancamento = Lancamento.Criar(TipoLancamento.Debito, 75.50m, "Conta de água", data);

        var evento = lancamento.DomainEvents.OfType<LancamentoCriadoEvent>().Single();
        evento.LancamentoId.Should().Be(lancamento.Id);
        evento.Tipo.Should().Be(TipoLancamento.Debito);
        evento.Valor.Should().Be(75.50m);
        evento.Data.Should().Be(data);
    }

    [Fact]
    public void Remover_DevePublicarLancamentoRemovidoEvent()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Debito, 80m, "Compra fornecedor", DateOnly.FromDateTime(DateTime.Today));
        lancamento.ClearDomainEvents();

        lancamento.Remover();

        lancamento.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LancamentoRemovidoEvent>();
    }

    [Fact]
    public void Remover_EventoDeveConterDadosOriginaisDoLancamento()
    {
        var data = new DateOnly(2025, 6, 1);
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 200m, "Venda cartão", data);
        lancamento.ClearDomainEvents();

        lancamento.Remover();

        var evento = lancamento.DomainEvents.OfType<LancamentoRemovidoEvent>().Single();
        evento.LancamentoId.Should().Be(lancamento.Id);
        evento.Tipo.Should().Be(TipoLancamento.Credito);
        evento.Valor.Should().Be(200m);
        evento.Data.Should().Be(data);
    }

    [Fact]
    public void ClearDomainEvents_DeveRemoverTodosOsEventos()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, "Venda", DateOnly.FromDateTime(DateTime.Today));

        lancamento.ClearDomainEvents();

        lancamento.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Criar_DescricaoComEspacos_DeveAparar()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, "  Venda  ", DateOnly.FromDateTime(DateTime.Today));

        lancamento.Descricao.Should().Be("Venda");
    }

    // ── Dinheiro ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dinheiro_ComValoresIguais_DeveSerIgual()
    {
        var d1 = Dinheiro.Criar(99.99m);
        var d2 = Dinheiro.Criar(99.99m);

        d1.Should().Be(d2);
    }

    [Fact]
    public void Dinheiro_ComValoresDiferentes_NaoDeveSerIgual()
    {
        var d1 = Dinheiro.Criar(100m);
        var d2 = Dinheiro.Criar(200m);

        d1.Should().NotBe(d2);
    }

    [Fact]
    public void Dinheiro_DeveArredondarParaDuasCasas()
    {
        var dinheiro = Dinheiro.Criar(10.999m);

        dinheiro.Valor.Should().Be(11.00m);
    }

    [Fact]
    public void Dinheiro_ConversaoImplicita_DeveRetornarDecimal()
    {
        var dinheiro = Dinheiro.Criar(50m);

        decimal valor = dinheiro;

        valor.Should().Be(50m);
    }
}
