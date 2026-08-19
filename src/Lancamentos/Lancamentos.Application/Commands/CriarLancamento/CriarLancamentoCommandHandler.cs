using Lancamentos.Application.DTOs;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Repositories;
using MediatR;
using SharedKernel;

namespace Lancamentos.Application.Commands.CriarLancamento;

public sealed class CriarLancamentoCommandHandler
    : IRequestHandler<CriarLancamentoCommand, LancamentoDto>
{
    private readonly ILancamentoRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public CriarLancamentoCommandHandler(
        ILancamentoRepository repository,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task<LancamentoDto> Handle(CriarLancamentoCommand request, CancellationToken cancellationToken)
    {
        var lancamento = Lancamento.Criar(request.Tipo, request.Valor, request.Descricao, request.Data);

        await _repository.AddAsync(lancamento, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        foreach (var @event in lancamento.DomainEvents)
            await _publisher.Publish(@event, cancellationToken);

        lancamento.ClearDomainEvents();

        return new LancamentoDto(
            lancamento.Id,
            lancamento.Tipo,
            lancamento.Valor.Valor,
            lancamento.Descricao,
            lancamento.Data,
            lancamento.CriadoEm);
    }
}
