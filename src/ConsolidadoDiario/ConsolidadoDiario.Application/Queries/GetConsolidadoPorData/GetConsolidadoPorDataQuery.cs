using ConsolidadoDiario.Application.DTOs;
using MediatR;

namespace ConsolidadoDiario.Application.Queries.GetConsolidadoPorData;

public sealed record GetConsolidadoPorDataQuery(DateOnly Data) : IRequest<ConsolidadoDto?>;
