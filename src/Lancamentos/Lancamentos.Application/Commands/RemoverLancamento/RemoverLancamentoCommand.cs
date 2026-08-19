using MediatR;

namespace Lancamentos.Application.Commands.RemoverLancamento;

public sealed record RemoverLancamentoCommand(Guid Id) : IRequest;
