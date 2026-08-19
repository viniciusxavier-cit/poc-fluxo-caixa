using Lancamentos.Application.DTOs;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorData;

public sealed record GetLancamentoPorDataQuery(DateOnly Data) : IRequest<IReadOnlyList<LancamentoDto>>;
