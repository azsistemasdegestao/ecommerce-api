namespace Ecommerce.Domain.Events;

public sealed record UserRegistered(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Email) : IDomainEvent;
