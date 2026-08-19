using Lancamentos.Application.DTOs;
using SharedKernel;
using MediatR;

namespace Lancamentos.Application.Commands.CriarLancamento;

public sealed record CriarLancamentoCommand(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data) : IRequest<LancamentoDto>;
