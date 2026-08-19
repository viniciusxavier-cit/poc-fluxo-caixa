using FluentAssertions;
using Lancamentos.Application.Commands.CriarLancamento;
using Xunit;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharedKernel;

namespace Lancamentos.UnitTests.Application;

public sealed class CriarLancamentoCommandHandlerTests
{
    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly CriarLancamentoCommandHandler _handler;

    public CriarLancamentoCommandHandlerTests()
    {
        _handler = new CriarLancamentoCommandHandler(_repository, _unitOfWork, _publisher);
    }

    [Fact]
    public async Task Handle_ComDadosValidos_DeveRetornarLancamentoDto()
    {
        var command = new CriarLancamentoCommand(
            TipoLancamento.Credito, 150m, "Venda online", DateOnly.FromDateTime(DateTime.Today));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Tipo.Should().Be(TipoLancamento.Credito);
        result.Valor.Should().Be(150m);
        result.Descricao.Should().Be("Venda online");
    }

    [Fact]
    public async Task Handle_DeveAdicionarLancamentoNoRepositorio()
    {
        var command = new CriarLancamentoCommand(
            TipoLancamento.Debito, 50m, "Pagamento luz", DateOnly.FromDateTime(DateTime.Today));

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<Lancamento>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeveFazerCommit()
    {
        var command = new CriarLancamentoCommand(
            TipoLancamento.Credito, 200m, "TED recebida", DateOnly.FromDateTime(DateTime.Today));

        await _handler.Handle(command, CancellationToken.None);

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DevePublicarDomainEvent()
    {
        var command = new CriarLancamentoCommand(
            TipoLancamento.Credito, 300m, "Boleto recebido", DateOnly.FromDateTime(DateTime.Today));

        await _handler.Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommitAntesDaPublicacaoDoEvento()
    {
        // Garante que a persistência ocorre ANTES da publicação do evento (resiliência)
        var ordem = new List<string>();

        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { ordem.Add("commit"); return Task.FromResult(1); });

        _publisher.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(_ => { ordem.Add("publish"); return Task.CompletedTask; });

        var command = new CriarLancamentoCommand(
            TipoLancamento.Credito, 100m, "Teste ordem", DateOnly.FromDateTime(DateTime.Today));

        await _handler.Handle(command, CancellationToken.None);

        ordem.Should().ContainInOrder("commit", "publish");
    }

    [Fact]
    public async Task Handle_FalhaNoCommit_DevePropagarExcecao()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Banco indisponível"));

        var command = new CriarLancamentoCommand(
            TipoLancamento.Debito, 100m, "Pagamento", DateOnly.FromDateTime(DateTime.Today));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("Banco indisponível");
    }

    [Fact]
    public async Task Handle_FalhaNoCommit_NaoDevePublicarEvento()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Banco indisponível"));

        var command = new CriarLancamentoCommand(
            TipoLancamento.Debito, 100m, "Pagamento", DateOnly.FromDateTime(DateTime.Today));

        await _handler.Invoking(h => h.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<Exception>();

        await _publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeveRetornarIdUnico_CadaChamada()
    {
        var command = new CriarLancamentoCommand(
            TipoLancamento.Credito, 100m, "Venda", DateOnly.FromDateTime(DateTime.Today));

        var r1 = await _handler.Handle(command, CancellationToken.None);
        var r2 = await _handler.Handle(command, CancellationToken.None);

        r1.Id.Should().NotBe(r2.Id);
    }
}
