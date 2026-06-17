using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ecommerce.Infrastructure.HealthChecks;

public sealed class EventBusHealthCheck : IHealthCheck
{
    private readonly IEventBus _eventBus;

    public EventBusHealthCheck(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(HealthCheckResult.Healthy($"{_eventBus.GetType().Name} is registered and resolvable."));
}
