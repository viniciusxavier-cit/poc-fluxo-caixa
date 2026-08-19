using FluentValidation;
using SharedKernel;

namespace Lancamentos.Application.Commands.CriarLancamento;

public sealed class CriarLancamentoCommandValidator : AbstractValidator<CriarLancamentoCommand>
{
    public CriarLancamentoCommandValidator()
    {
        RuleFor(x => x.Tipo)
            .IsInEnum()
            .WithMessage("Tipo de lançamento inválido. Use 1 (Crédito) ou 2 (Débito).");

        RuleFor(x => x.Valor)
            .GreaterThan(0)
            .WithMessage("O valor deve ser maior que zero.")
            .LessThanOrEqualTo(1_000_000_000)
            .WithMessage("O valor não pode exceder R$ 1.000.000.000,00.")
            .PrecisionScale(18, 2, false)
            .WithMessage("O valor deve ter no máximo 2 casas decimais.");

        RuleFor(x => x.Descricao)
            .NotEmpty()
            .WithMessage("A descrição é obrigatória.")
            .MaximumLength(255)
            .WithMessage("A descrição não pode exceder 255 caracteres.");

        RuleFor(x => x.Data)
            .NotEmpty()
            .WithMessage("A data é obrigatória.")
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("Não é permitido registrar lançamentos com data futura.");
    }
}
