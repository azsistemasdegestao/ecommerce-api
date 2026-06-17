namespace Ecommerce.Domain.Events;

public sealed record ProductUpdated(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string Slug) : IDomainEvent;
