using Lancamentos.Application.Commands.CriarLancamento;
using Lancamentos.Application.Commands.RemoverLancamento;
using Lancamentos.Application.DTOs;
using Lancamentos.Application.Queries.GetLancamentoPorData;
using Lancamentos.Application.Queries.GetLancamentoPorId;
using Lancamentos.Application.Queries.GetLancamentoPorIntervalo;
using SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Lancamentos.API.Endpoints;

public static class LancamentosEndpoints
{
    public static void MapLancamentosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lancamentos")
            .WithTags("Lançamentos")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapPost("/", async (CriarLancamentoRequest req, IMediator mediator, CancellationToken ct) =>
        {
            var command = new CriarLancamentoCommand(req.Tipo, req.Valor, req.Descricao, req.Data);
            var result = await mediator.Send(command, ct);
            return Results.Created($"/lancamentos/{result.Id}", result);
        })
        .WithName("CriarLancamento")
        .WithSummary("Registra um lançamento")
        .WithDescription("Cria um débito ou crédito no fluxo de caixa. O consolidado diário é atualizado automaticamente via domain event.")
        .Produces<LancamentoDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetLancamentoPorIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetLancamentoPorId")
        .WithSummary("Busca um lançamento pelo ID")
        .Produces<LancamentoDto>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithOpenApi();

        group.MapGet("/", async (
            [FromQuery] DateOnly? data,
            [FromQuery] DateOnly? dataInicio,
            [FromQuery] DateOnly? dataFim,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (dataInicio.HasValue || dataFim.HasValue)
            {
                var inicio = dataInicio ?? DateOnly.MinValue;
                var fim = dataFim ?? DateOnly.MaxValue;
                var result = await mediator.Send(new GetLancamentoPorIntervaloQuery(inicio, fim), ct);
                return Results.Ok(result);
            }

            if (data.HasValue)
            {
                var result = await mediator.Send(new GetLancamentoPorDataQuery(data.Value), ct);
                return Results.Ok(result);
            }

            return Results.BadRequest(new { status = 400, title = "Parâmetro obrigatório", detail = "Informe 'data' ou 'dataInicio'/'dataFim'." });
        })
        .WithName("ListarLancamentos")
        .WithSummary("Lista lançamentos por data ou intervalo")
        .WithDescription(
            "Aceita três formas de consulta:\n" +
            "1. **data** (yyyy-MM-dd) — retorna lançamentos de um único dia (comportamento original).\n" +
            "2. **dataInicio** + **dataFim** (yyyy-MM-dd) — retorna todos os lançamentos no intervalo, inclusive os extremos.\n" +
            "3. Nenhum parâmetro — retorna 400 Bad Request.")
        .Produces<IReadOnlyList<LancamentoDto>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithOpenApi(op =>
        {
            var pData = op.Parameters.FirstOrDefault(p => p.Name == "data");
            if (pData is not null) pData.Description = "Data única no formato yyyy-MM-dd (ex: 2025-06-01). Mutuamente exclusivo com dataInicio/dataFim.";
            var pInicio = op.Parameters.FirstOrDefault(p => p.Name == "dataInicio");
            if (pInicio is not null) pInicio.Description = "Data de início do intervalo no formato yyyy-MM-dd (ex: 2025-06-01). Deve ser usada junto com dataFim.";
            var pFim = op.Parameters.FirstOrDefault(p => p.Name == "dataFim");
            if (pFim is not null) pFim.Description = "Data de fim do intervalo no formato yyyy-MM-dd (ex: 2025-06-30). Não pode ser anterior a dataInicio.";
            return op;
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new RemoverLancamentoCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("RemoverLancamento")
        .WithSummary("Remove um lançamento")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithOpenApi();
    }
}

public sealed record CriarLancamentoRequest(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data);
