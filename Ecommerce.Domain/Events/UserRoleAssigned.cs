namespace Ecommerce.Domain.Events;

public sealed record UserRoleAssigned(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Role) : IDomainEvent;
