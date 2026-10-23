using Lancamentos.Application.DTOs;
using Lancamentos.Domain.Repositories;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorIntervalo;

public sealed class GetLancamentoPorIntervaloQueryHandler
    : IRequestHandler<GetLancamentoPorIntervaloQuery, IReadOnlyList<LancamentoDto>>
{
    private readonly ILancamentoRepository _repository;

    public GetLancamentoPorIntervaloQueryHandler(ILancamentoRepository repository)
        => _repository = repository;

    public async Task<IReadOnlyList<LancamentoDto>> Handle(
        GetLancamentoPorIntervaloQuery request,
        CancellationToken cancellationToken)
    {
        var lancamentos = await _repository.GetByIntervaloAsync(
            request.DataInicio, request.DataFim, cancellationToken);

        return lancamentos
            .Select(l => new LancamentoDto(l.Id, l.Tipo, l.Valor.Valor, l.Descricao, l.Data, l.CriadoEm))
            .ToList()
            .AsReadOnly();
    }
}
