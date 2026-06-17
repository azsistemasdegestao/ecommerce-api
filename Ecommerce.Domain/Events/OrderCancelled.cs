namespace Ecommerce.Domain.Events;

public sealed record OrderCancelled(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId) : IDomainEvent;
