namespace Ecommerce.Domain.Events;

public sealed record OrderStatusUpdated(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string OldStatus,
    string NewStatus) : IDomainEvent;
