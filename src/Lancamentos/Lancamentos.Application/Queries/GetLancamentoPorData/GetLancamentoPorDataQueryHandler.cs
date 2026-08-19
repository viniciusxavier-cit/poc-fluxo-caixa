using Lancamentos.Application.DTOs;
using Lancamentos.Domain.Repositories;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorData;

public sealed class GetLancamentoPorDataQueryHandler
    : IRequestHandler<GetLancamentoPorDataQuery, IReadOnlyList<LancamentoDto>>
{
    private readonly ILancamentoRepository _repository;

    public GetLancamentoPorDataQueryHandler(ILancamentoRepository repository)
        => _repository = repository;

    public async Task<IReadOnlyList<LancamentoDto>> Handle(
        GetLancamentoPorDataQuery request,
        CancellationToken cancellationToken)
    {
        var lancamentos = await _repository.GetByDataAsync(request.Data, cancellationToken);

        return lancamentos
            .Select(l => new LancamentoDto(l.Id, l.Tipo, l.Valor.Valor, l.Descricao, l.Data, l.CriadoEm))
            .ToList()
            .AsReadOnly();
    }
}
