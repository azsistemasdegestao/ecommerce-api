namespace Ecommerce.Domain.Events;

public sealed record UserLoggedIn(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Email) : IDomainEvent;
