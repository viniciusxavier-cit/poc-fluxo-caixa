using FluentAssertions;
using Lancamentos.Application.Queries.GetLancamentoPorIntervalo;
using Xunit;

namespace Lancamentos.UnitTests.Application;

public sealed class GetLancamentoPorIntervaloQueryValidatorTests
{
    private readonly GetLancamentoPorIntervaloQueryValidator _validator = new();

    [Fact]
    public async Task Validate_IntervaloValido_DevePassar()
    {
        var query = new GetLancamentoPorIntervaloQuery(
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30));

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_MesmaDia_DevePassar()
    {
        var data = new DateOnly(2025, 6, 15);
        var query = new GetLancamentoPorIntervaloQuery(data, data);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_DataFimAnteriorADataInicio_DeveRetornarErro()
    {
        var query = new GetLancamentoPorIntervaloQuery(
            new DateOnly(2025, 6, 30),
            new DateOnly(2025, 6, 1));

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.DataFim) &&
            e.ErrorMessage == "A data de fim não pode ser anterior à data de início.");
    }

    [Fact]
    public async Task Validate_DataInicioDefault_DeveRetornarErro()
    {
        var query = new GetLancamentoPorIntervaloQuery(
            default,
            new DateOnly(2025, 6, 30));

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.DataInicio) &&
            e.ErrorMessage == "A data de início é obrigatória.");
    }

    [Fact]
    public async Task Validate_DataFimDefault_DeveRetornarErro()
    {
        var query = new GetLancamentoPorIntervaloQuery(
            new DateOnly(2025, 6, 1),
            default);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(query.DataFim));
    }
}
