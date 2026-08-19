using MediatR;

namespace SharedKernel;

public interface IDomainEvent : INotification
{
    DateTime OcorridoEm { get; }
}
