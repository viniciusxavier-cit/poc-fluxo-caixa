using ConsolidadoDiario.Application.DTOs;
using ConsolidadoDiario.Application.Queries.GetConsolidadoPorData;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace ConsolidadoDiario.API.Endpoints;

public static class ConsolidadoEndpoints
{
    private const int CacheTtlSeconds = 60;

    public static void MapConsolidadoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/consolidado")
            .WithTags("Consolidado Diário")
            .RequireAuthorization()
            .RequireRateLimiting("consolidado");

        group.MapGet("/{data}", async (
            DateOnly data,
            IMediator mediator,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var cacheKey = $"consolidado:{data:yyyy-MM-dd}";

            if (cache.TryGetValue(cacheKey, out ConsolidadoDto? cached))
                return cached is null
                    ? Results.NotFound(new { mensagem = $"Nenhum consolidado encontrado para {data:yyyy-MM-dd}." })
                    : Results.Ok(cached);

            var result = await mediator.Send(new GetConsolidadoPorDataQuery(data), ct);

            if (result is not null)
                cache.Set(cacheKey, result, TimeSpan.FromSeconds(CacheTtlSeconds));

            return result is null
                ? Results.NotFound(new { mensagem = $"Nenhum consolidado encontrado para {data:yyyy-MM-dd}." })
                : Results.Ok(result);
        })
        .WithName("GetConsolidadoDiario")
        .WithSummary("Saldo consolidado de uma data")
        .WithDescription(
            "Retorna o total de créditos, débitos e saldo líquido consolidado do dia informado. " +
            "Resultado cacheado por 60 segundos para suportar alta carga.")
        .Produces<ConsolidadoDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status429TooManyRequests)
        .WithOpenApi(op =>
        {
            op.Parameters[0].Description = "Data no formato yyyy-MM-dd (ex: 2025-06-01)";
            return op;
        });
    }
}
