namespace Ecommerce.Domain.Events;

public sealed record PaymentFailed(
    Guid EventId,
    DateTime OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    string Reason) : IDomainEvent;
