using Lancamentos.Application.Commands.CriarLancamento;
using Lancamentos.Application.Commands.RemoverLancamento;
using Lancamentos.Application.DTOs;
using Lancamentos.Application.Queries.GetLancamentoPorData;
using Lancamentos.Application.Queries.GetLancamentoPorId;
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

        group.MapGet("/", async ([FromQuery] DateOnly data, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetLancamentoPorDataQuery(data), ct);
            return Results.Ok(result);
        })
        .WithName("ListarLancamentosPorData")
        .WithSummary("Lista lançamentos de uma data")
        .Produces<IReadOnlyList<LancamentoDto>>()
        .WithOpenApi(op =>
        {
            op.Parameters[0].Description = "Data no formato yyyy-MM-dd (ex: 2025-06-01)";
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
