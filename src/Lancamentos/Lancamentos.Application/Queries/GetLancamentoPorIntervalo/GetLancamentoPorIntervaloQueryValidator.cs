using FluentValidation;

namespace Lancamentos.Application.Queries.GetLancamentoPorIntervalo;

public sealed class GetLancamentoPorIntervaloQueryValidator : AbstractValidator<GetLancamentoPorIntervaloQuery>
{
    public GetLancamentoPorIntervaloQueryValidator()
    {
        RuleFor(x => x.DataInicio)
            .NotEmpty()
            .WithMessage("A data de início é obrigatória.");

        RuleFor(x => x.DataFim)
            .NotEmpty()
            .WithMessage("A data de fim é obrigatória.")
            .GreaterThanOrEqualTo(x => x.DataInicio)
            .WithMessage("A data de fim não pode ser anterior à data de início.");
    }
}
