using ConsolidadoDiario.Domain.Repositories;
using Lancamentos.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ConsolidadoDiario.Application.EventHandlers;

public sealed class RecalcularConsolidadoEventHandler : INotificationHandler<LancamentoRemovidoEvent>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly IConsolidadoUnitOfWork _unitOfWork;
    private readonly ILogger<RecalcularConsolidadoEventHandler> _logger;

    public RecalcularConsolidadoEventHandler(
        IConsolidadoDiarioRepository repository,
        IConsolidadoUnitOfWork unitOfWork,
        ILogger<RecalcularConsolidadoEventHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(LancamentoRemovidoEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var consolidado = await _repository.GetByDataAsync(notification.Data, cancellationToken);
            if (consolidado is null) return;

            consolidado.EstornarLancamento(notification.Tipo, notification.Valor);

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Consolidado de {Data} recalculado após remoção do lançamento {Id}.",
                notification.Data, notification.LancamentoId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao recalcular consolidado para data {Data}.",
                notification.Data);
        }
    }
}
