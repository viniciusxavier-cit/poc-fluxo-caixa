using ConsolidadoDiario.Application.EventHandlers;
using ConsolidadoDiario.Domain.Repositories;
using ConsolidadoDiario.Infrastructure;
using ConsolidadoDiario.Infrastructure.Persistence;
using FluentAssertions;
using Lancamentos.Domain.Events;
using SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsolidadoDiario.UnitTests.Application;

public sealed class AtualizarConsolidadoEventHandlerTests : IDisposable
{
    private readonly ConsolidadoDbContext _dbContext;
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ConsolidadoDiario.Domain.Repositories.IConsolidadoUnitOfWork _unitOfWork;
    private readonly AtualizarConsolidadoEventHandler _handler;
    private readonly RecalcularConsolidadoEventHandler _recalcularHandler;

    public AtualizarConsolidadoEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ConsolidadoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ConsolidadoDbContext(options);
        _repository = new ConsolidadoDiario.Infrastructure.Repositories.ConsolidadoDiarioRepository(_dbContext);
        _unitOfWork = new ConsolidadoUnitOfWork(_dbContext);
        _handler = new AtualizarConsolidadoEventHandler(
            _repository, _unitOfWork, NullLogger<AtualizarConsolidadoEventHandler>.Instance);
        _recalcularHandler = new RecalcularConsolidadoEventHandler(
            _repository, _unitOfWork, NullLogger<RecalcularConsolidadoEventHandler>.Instance);
    }

    // ── Criação ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PrimeiroLancamento_DeveCriarConsolidado()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);
        var @event = new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 500m, data, DateTime.UtcNow);

        await _handler.Handle(@event, CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado.Should().NotBeNull();
        consolidado!.TotalCreditos.Should().Be(500m);
        consolidado.TotalDebitos.Should().Be(0m);
        consolidado.Saldo.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_LancamentosMultiplos_DeveAcumularCorretamente()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 1000m, data, DateTime.UtcNow),
            CancellationToken.None);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Debito, 300m, data, DateTime.UtcNow),
            CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.TotalCreditos.Should().Be(1000m);
        consolidado.TotalDebitos.Should().Be(300m);
        consolidado.Saldo.Should().Be(700m);
    }

    [Fact]
    public async Task Handle_LancamentosEmDatasDistintas_DeveCriarConsolidadosSeparados()
    {
        var data1 = new DateOnly(2025, 6, 1);
        var data2 = new DateOnly(2025, 6, 2);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 400m, data1, DateTime.UtcNow),
            CancellationToken.None);
        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 600m, data2, DateTime.UtcNow),
            CancellationToken.None);

        var c1 = await _repository.GetByDataAsync(data1);
        var c2 = await _repository.GetByDataAsync(data2);

        c1!.TotalCreditos.Should().Be(400m);
        c2!.TotalCreditos.Should().Be(600m);
        c1.Saldo.Should().NotBe(c2.Saldo);
    }

    [Fact]
    public async Task Handle_DebitoMaiorQueCredito_SaldoDeveSerNegativo()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 100m, data, DateTime.UtcNow),
            CancellationToken.None);
        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Debito, 300m, data, DateTime.UtcNow),
            CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.Saldo.Should().Be(-200m);
    }

    // ── Estorno / Remoção ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecalcularHandler_EstornoDeCredito_DeveReduzirTotalCreditos()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 1000m, data, DateTime.UtcNow),
            CancellationToken.None);

        var lancamentoId = Guid.NewGuid();
        await _recalcularHandler.Handle(
            new LancamentoRemovidoEvent(lancamentoId, TipoLancamento.Credito, 400m, data, DateTime.UtcNow),
            CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.TotalCreditos.Should().Be(600m);
        consolidado.Saldo.Should().Be(600m);
    }

    [Fact]
    public async Task RecalcularHandler_EstornoDeDebito_DeveReduzirTotalDebitos()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Debito, 500m, data, DateTime.UtcNow),
            CancellationToken.None);

        await _recalcularHandler.Handle(
            new LancamentoRemovidoEvent(Guid.NewGuid(), TipoLancamento.Debito, 200m, data, DateTime.UtcNow),
            CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.TotalDebitos.Should().Be(300m);
    }

    [Fact]
    public async Task RecalcularHandler_SemConsolidadoParaData_NaoDeveLancarExcecao()
    {
        var dataInexistente = new DateOnly(2020, 1, 1);

        var act = () => _recalcularHandler.Handle(
            new LancamentoRemovidoEvent(Guid.NewGuid(), TipoLancamento.Credito, 100m, dataInexistente, DateTime.UtcNow),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecalcularHandler_EstornoMaiorQueTotal_NaoDeveResultarEmValorNegativo()
    {
        var data = DateOnly.FromDateTime(DateTime.Today);

        await _handler.Handle(
            new LancamentoCriadoEvent(Guid.NewGuid(), TipoLancamento.Credito, 100m, data, DateTime.UtcNow),
            CancellationToken.None);

        await _recalcularHandler.Handle(
            new LancamentoRemovidoEvent(Guid.NewGuid(), TipoLancamento.Credito, 9999m, data, DateTime.UtcNow),
            CancellationToken.None);

        var consolidado = await _repository.GetByDataAsync(data);
        consolidado!.TotalCreditos.Should().Be(0m);
    }

    // ── Domínio do ConsolidadoDiario ──────────────────────────────────────────

    [Fact]
    public void ConsolidadoDiario_SaldoECalculado_NaoArmazenado()
    {
        var consolidado = ConsolidadoDiario.Domain.Entities.ConsolidadoDiario.Criar(DateOnly.FromDateTime(DateTime.Today));
        consolidado.AplicarLancamento(TipoLancamento.Credito, 500m);
        consolidado.AplicarLancamento(TipoLancamento.Debito, 150m);

        consolidado.Saldo.Should().Be(350m);
        consolidado.TotalCreditos.Should().Be(500m);
        consolidado.TotalDebitos.Should().Be(150m);
    }

    [Fact]
    public void ConsolidadoDiario_AplicarLancamentoComValorZero_DeveLancarExcecao()
    {
        var consolidado = ConsolidadoDiario.Domain.Entities.ConsolidadoDiario.Criar(DateOnly.FromDateTime(DateTime.Today));

        var act = () => consolidado.AplicarLancamento(TipoLancamento.Credito, 0m);

        act.Should().Throw<ArgumentException>();
    }

    public void Dispose() => _dbContext.Dispose();
}
