namespace Ecommerce.Domain.Events;

public sealed record PaymentRefunded(
    Guid EventId,
    DateTime OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount) : IDomainEvent;
