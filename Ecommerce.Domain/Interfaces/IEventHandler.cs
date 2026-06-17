using Ecommerce.Domain.Events;

namespace Ecommerce.Domain.Interfaces;

public interface IEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken ct = default);
}
