using ConsolidadoDiario.Domain.Repositories;
using Lancamentos.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ConsolidadoDiario.Application.EventHandlers;

public sealed class AtualizarConsolidadoEventHandler : INotificationHandler<LancamentoCriadoEvent>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly IConsolidadoUnitOfWork _unitOfWork;
    private readonly ILogger<AtualizarConsolidadoEventHandler> _logger;

    public AtualizarConsolidadoEventHandler(
        IConsolidadoDiarioRepository repository,
        IConsolidadoUnitOfWork unitOfWork,
        ILogger<AtualizarConsolidadoEventHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(LancamentoCriadoEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var consolidado = await _repository.GetByDataAsync(notification.Data, cancellationToken);

            if (consolidado is null)
            {
                consolidado = ConsolidadoDiario.Domain.Entities.ConsolidadoDiario.Criar(notification.Data);
                await _repository.AddAsync(consolidado, cancellationToken);
            }

            consolidado.AplicarLancamento(notification.Tipo, notification.Valor);
            _repository.Update(consolidado);

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Consolidado de {Data} atualizado. Saldo: {Saldo}",
                notification.Data, consolidado.Saldo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao atualizar consolidado para data {Data}. O lançamento {LancamentoId} foi persistido.",
                notification.Data, notification.LancamentoId);
        }
    }
}
