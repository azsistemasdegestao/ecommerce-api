using Ecommerce.Application.Common.Observability;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.EventBus;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken ct = default)
        where T : IDomainEvent
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity($"Publish {typeof(T).Name}");

        _logger.LogInformation(
            "Publishing event {EventType} with id {EventId}",
            typeof(T).Name,
            domainEvent.EventId);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<T>>();

        foreach (var handler in handlers)
            await handler.HandleAsync(domainEvent, ct);
    }
}
