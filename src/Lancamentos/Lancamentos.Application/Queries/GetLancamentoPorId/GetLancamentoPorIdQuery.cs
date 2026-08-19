using Lancamentos.Application.DTOs;
using MediatR;

namespace Lancamentos.Application.Queries.GetLancamentoPorId;

public sealed record GetLancamentoPorIdQuery(Guid Id) : IRequest<LancamentoDto?>;
