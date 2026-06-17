using Ecommerce.Domain.Events;

namespace Ecommerce.Domain.Interfaces;

public interface IEventBus
{
    Task PublishAsync<T>(T domainEvent, CancellationToken ct = default)
        where T : IDomainEvent;
}
