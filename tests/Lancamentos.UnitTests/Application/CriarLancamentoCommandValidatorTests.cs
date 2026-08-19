using FluentAssertions;
using Lancamentos.Application.Commands.CriarLancamento;
using SharedKernel;
using Xunit;

namespace Lancamentos.UnitTests.Application;

public sealed class CriarLancamentoCommandValidatorTests
{
    private readonly CriarLancamentoCommandValidator _validator = new();
    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Validate_ComDadosValidos_DevePassar()
    {
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, 100m, "Venda", Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task Validate_ComValorInvalido_DeveRetornarErro(decimal valor)
    {
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, valor, "Venda", Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Valor));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_ComDescricaoVazia_DeveRetornarErro(string descricao)
    {
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, 100m, descricao, Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Descricao));
    }

    [Fact]
    public async Task Validate_ComDescricaoAcimaDe255Chars_DeveRetornarErro()
    {
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, 100m, new string('x', 256), Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Descricao));
    }

    [Fact]
    public async Task Validate_ComDataFutura_DeveRetornarErro()
    {
        var dataFutura = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, 100m, "Venda", dataFutura);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Data));
    }

    [Fact]
    public async Task Validate_ComValorAcimaDoLimite_DeveRetornarErro()
    {
        var command = new CriarLancamentoCommand(TipoLancamento.Credito, 1_000_000_001m, "Venda", Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Valor));
    }

    [Fact]
    public async Task Validate_ComTipoInvalido_DeveRetornarErro()
    {
        var command = new CriarLancamentoCommand((TipoLancamento)99, 100m, "Venda", Hoje);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Tipo));
    }
}
