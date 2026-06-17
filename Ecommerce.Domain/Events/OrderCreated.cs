namespace Ecommerce.Domain.Events;

public sealed record OrderCreated(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid UserId,
    decimal Total) : IDomainEvent;
