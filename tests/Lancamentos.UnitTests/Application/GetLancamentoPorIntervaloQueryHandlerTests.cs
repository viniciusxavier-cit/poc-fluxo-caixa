using FluentAssertions;
using Lancamentos.Application.Queries.GetLancamentoPorIntervalo;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using NSubstitute;
using SharedKernel;
using Xunit;

namespace Lancamentos.UnitTests.Application;

public sealed class GetLancamentoPorIntervaloQueryHandlerTests
{
    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly GetLancamentoPorIntervaloQueryHandler _handler;

    public GetLancamentoPorIntervaloQueryHandlerTests()
    {
        _handler = new GetLancamentoPorIntervaloQueryHandler(_repository);
    }

    [Fact]
    public async Task Handle_IntervaloValido_DeveRetornarLancamentosNoPeriodo()
    {
        var inicio = new DateOnly(2025, 6, 1);
        var fim = new DateOnly(2025, 6, 30);
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, "Venda", inicio);
        _repository.GetByIntervaloAsync(inicio, fim, Arg.Any<CancellationToken>())
            .Returns([lancamento]);

        var result = await _handler.Handle(new GetLancamentoPorIntervaloQuery(inicio, fim), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Valor.Should().Be(100m);
        result[0].Descricao.Should().Be("Venda");
    }

    [Fact]
    public async Task Handle_IntervaloSemLancamentos_DeveRetornarListaVazia()
    {
        var inicio = new DateOnly(2025, 1, 1);
        var fim = new DateOnly(2025, 1, 31);
        _repository.GetByIntervaloAsync(inicio, fim, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _handler.Handle(new GetLancamentoPorIntervaloQuery(inicio, fim), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_IntervaloMesmoDia_DeveRetornarLancamentosDessaDia()
    {
        var data = new DateOnly(2025, 6, 15);
        var lancamento = Lancamento.Criar(TipoLancamento.Debito, 50m, "Pagamento fornecedor", data);
        _repository.GetByIntervaloAsync(data, data, Arg.Any<CancellationToken>())
            .Returns([lancamento]);

        var result = await _handler.Handle(new GetLancamentoPorIntervaloQuery(data, data), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Tipo.Should().Be(TipoLancamento.Debito);
    }

    [Fact]
    public async Task Handle_DeveMapearTodosOsCamposCorretamente()
    {
        var data = new DateOnly(2025, 6, 10);
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 999.99m, "TED recebida", data);
        _repository.GetByIntervaloAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([lancamento]);

        var result = await _handler.Handle(
            new GetLancamentoPorIntervaloQuery(data, data), CancellationToken.None);

        result[0].Id.Should().Be(lancamento.Id);
        result[0].Tipo.Should().Be(TipoLancamento.Credito);
        result[0].Valor.Should().Be(999.99m);
        result[0].Descricao.Should().Be("TED recebida");
        result[0].Data.Should().Be(data);
    }

    [Fact]
    public async Task Handle_DeveChamarRepositorioComParametrosCorretos()
    {
        var inicio = new DateOnly(2025, 3, 1);
        var fim = new DateOnly(2025, 3, 31);
        _repository.GetByIntervaloAsync(inicio, fim, Arg.Any<CancellationToken>())
            .Returns([]);

        await _handler.Handle(new GetLancamentoPorIntervaloQuery(inicio, fim), CancellationToken.None);

        await _repository.Received(1).GetByIntervaloAsync(inicio, fim, Arg.Any<CancellationToken>());
    }
}
