namespace Ecommerce.Domain.Events;

public sealed record ProductCreated(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string Slug) : IDomainEvent;
