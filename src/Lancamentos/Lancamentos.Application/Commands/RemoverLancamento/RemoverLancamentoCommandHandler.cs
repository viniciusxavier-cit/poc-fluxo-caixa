using Lancamentos.Domain.Repositories;
using MediatR;
using SharedKernel;

namespace Lancamentos.Application.Commands.RemoverLancamento;

public sealed class RemoverLancamentoCommandHandler : IRequestHandler<RemoverLancamentoCommand>
{
    private readonly ILancamentoRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public RemoverLancamentoCommandHandler(
        ILancamentoRepository repository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task Handle(RemoverLancamentoCommand request, CancellationToken cancellationToken)
    {
        var lancamento = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Lançamento {request.Id} não encontrado.");

        lancamento.Remover();
        _repository.Remove(lancamento);
        await _unitOfWork.CommitAsync(cancellationToken);

        foreach (var @event in lancamento.DomainEvents)
            await _publisher.Publish(@event, cancellationToken);

        lancamento.ClearDomainEvents();
    }
}
