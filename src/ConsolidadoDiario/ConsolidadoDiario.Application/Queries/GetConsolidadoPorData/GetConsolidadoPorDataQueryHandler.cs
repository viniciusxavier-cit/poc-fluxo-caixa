using ConsolidadoDiario.Application.DTOs;
using ConsolidadoDiario.Domain.Repositories;
using MediatR;

namespace ConsolidadoDiario.Application.Queries.GetConsolidadoPorData;

public sealed class GetConsolidadoPorDataQueryHandler
    : IRequestHandler<GetConsolidadoPorDataQuery, ConsolidadoDto?>
{
    private readonly IConsolidadoDiarioRepository _repository;

    public GetConsolidadoPorDataQueryHandler(IConsolidadoDiarioRepository repository)
        => _repository = repository;

    public async Task<ConsolidadoDto?> Handle(GetConsolidadoPorDataQuery request, CancellationToken cancellationToken)
    {
        var consolidado = await _repository.GetByDataAsync(request.Data, cancellationToken);
        if (consolidado is null) return null;

        return new ConsolidadoDto(
            consolidado.Id,
            consolidado.Data,
            consolidado.TotalCreditos,
            consolidado.TotalDebitos,
            consolidado.Saldo,
            consolidado.AtualizadoEm);
    }
}
