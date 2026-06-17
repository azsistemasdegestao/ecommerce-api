namespace Ecommerce.Domain.Events;

public sealed record ProductDeleted(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string Slug) : IDomainEvent;
