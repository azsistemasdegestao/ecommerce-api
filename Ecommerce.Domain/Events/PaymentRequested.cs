namespace Ecommerce.Domain.Events;

public sealed record PaymentRequested(
    Guid EventId,
    DateTime OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount) : IDomainEvent;
