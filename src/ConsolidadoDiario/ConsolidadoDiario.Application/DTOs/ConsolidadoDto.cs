namespace ConsolidadoDiario.Application.DTOs;

public sealed record ConsolidadoDto(
    Guid Id,
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal Saldo,
    DateTime AtualizadoEm);
