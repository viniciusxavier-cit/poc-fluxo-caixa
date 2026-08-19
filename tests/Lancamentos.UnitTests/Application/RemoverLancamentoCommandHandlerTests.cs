using FluentAssertions;
using Lancamentos.Application.Commands.RemoverLancamento;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using MediatR;
using Xunit;
using NSubstitute;
using SharedKernel;

namespace Lancamentos.UnitTests.Application;

public sealed class RemoverLancamentoCommandHandlerTests
{
    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly RemoverLancamentoCommandHandler _handler;

    public RemoverLancamentoCommandHandlerTests()
    {
        _handler = new RemoverLancamentoCommandHandler(_repository, _unitOfWork, _publisher);
    }

    [Fact]
    public async Task Handle_LancamentoExistente_DeveRemover()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Credito, 100m, "Venda", DateOnly.FromDateTime(DateTime.Today));
        _repository.GetByIdAsync(lancamento.Id, Arg.Any<CancellationToken>()).Returns(lancamento);

        await _handler.Handle(new RemoverLancamentoCommand(lancamento.Id), CancellationToken.None);

        _repository.Received(1).Remove(lancamento);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LancamentoInexistente_DeveLancarKeyNotFoundException()
    {
        var idInexistente = Guid.NewGuid();
        _repository.GetByIdAsync(idInexistente, Arg.Any<CancellationToken>()).Returns((Lancamento?)null);

        var act = () => _handler.Handle(new RemoverLancamentoCommand(idInexistente), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{idInexistente}*");
    }

    [Fact]
    public async Task Handle_LancamentoInexistente_NaoDevePublicarEvento()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Lancamento?)null);

        await _handler.Invoking(h => h.Handle(new RemoverLancamentoCommand(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();

        await _publisher.DidNotReceive().Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LancamentoExistente_DevePublicarLancamentoRemovidoEvent()
    {
        var lancamento = Lancamento.Criar(TipoLancamento.Debito, 200m, "Pagamento", DateOnly.FromDateTime(DateTime.Today));
        _repository.GetByIdAsync(lancamento.Id, Arg.Any<CancellationToken>()).Returns(lancamento);

        await _handler.Handle(new RemoverLancamentoCommand(lancamento.Id), CancellationToken.None);

        await _publisher.Received(1).Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }
}
