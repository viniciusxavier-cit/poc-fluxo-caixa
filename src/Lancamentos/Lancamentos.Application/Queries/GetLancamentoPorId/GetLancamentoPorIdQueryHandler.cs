using Lancamentos.Application.DTOs;
using Lancamentos.Domain.Repositories;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorId;

public sealed class GetLancamentoPorIdQueryHandler
    : IRequestHandler<GetLancamentoPorIdQuery, LancamentoDto?>
{
    private readonly ILancamentoRepository _repository;

    public GetLancamentoPorIdQueryHandler(ILancamentoRepository repository)
        => _repository = repository;

    public async Task<LancamentoDto?> Handle(GetLancamentoPorIdQuery request, CancellationToken cancellationToken)
    {
        var lancamento = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (lancamento is null) return null;

        return new LancamentoDto(
            lancamento.Id,
            lancamento.Tipo,
            lancamento.Valor.Valor,
            lancamento.Descricao,
            lancamento.Data,
            lancamento.CriadoEm);
    }
}
