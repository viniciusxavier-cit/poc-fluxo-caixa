using Lancamentos.Application.DTOs;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorIntervalo;

public sealed record GetLancamentoPorIntervaloQuery(DateOnly DataInicio, DateOnly DataFim)
    : IRequest<IReadOnlyList<LancamentoDto>>;
