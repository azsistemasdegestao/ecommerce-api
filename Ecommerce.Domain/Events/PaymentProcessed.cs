namespace Ecommerce.Domain.Events;

public sealed record PaymentProcessed(
    Guid EventId,
    DateTime OccurredAt,
    Guid PaymentId,
    Guid OrderId) : IDomainEvent;
