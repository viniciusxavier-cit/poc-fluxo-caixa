using SharedKernel;

namespace Lancamentos.Application.DTOs;

public sealed record LancamentoDto(
    Guid Id,
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data,
    DateTime CriadoEm);
