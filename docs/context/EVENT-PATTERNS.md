# EVENT-PATTERNS.md
> Global context document. Defines patterns, conventions, and flows for the project's event system.
> Skills must follow these patterns when generating events, handlers, and any event-driven code.

---

## Overview

The project uses **Domain Events** for asynchronous communication between modules.
Current implementation uses `InMemoryEventBus` (development), replaceable by RabbitMQ or Azure Service Bus without changing Domain or Application.

---

## Base Interfaces

```csharp
// Ecommerce.Domain/Events/IDomainEvent.cs
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

// Ecommerce.Domain/Interfaces/IEventBus.cs
public interface IEventBus
{
    Task PublishAsync<T>(T domainEvent, CancellationToken ct = default)
        where T : IDomainEvent;
}

// Ecommerce.Domain/Interfaces/IEventHandler.cs
public interface IEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken ct = default);
}
```

---

## Event Conventions

### Naming
```
[Entity][PastAction]

✅ OrderCreated
✅ PaymentProcessed
✅ ProductUpdated
✅ UserRegistered

❌ CreateOrder     (present tense — this is a Command, not an event)
❌ OnPaymentDone   (On prefix is forbidden)
❌ PaymentEvent    (Event suffix is redundant)
```

### Required structure
```csharp
// Always sealed record
// Always immutable (init only or record)
// Always with EventId and OccurredAt
public sealed record OrderCreated(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid UserId,
    decimal Total
) : IDomainEvent;
```

### Instantiation
```csharp
var @event = new OrderCreated(
    EventId: Guid.NewGuid(),
    OccurredAt: DateTime.UtcNow,
    OrderId: order.Id,
    UserId: order.UserId,
    Total: order.Total
);
```

---

## Handler Conventions

### Naming
```
[Event]Handler

✅ PaymentRequestedHandler
✅ OrderCreatedHandler
✅ ProductUpdatedHandler
```

### Required structure
```csharp
public sealed class PaymentRequestedHandler : IEventHandler<PaymentRequested>
{
    private readonly IMockGatewayService _gateway;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PaymentRequestedHandler> _logger;

    public PaymentRequestedHandler(
        IMockGatewayService gateway,
        IEventBus eventBus,
        ILogger<PaymentRequestedHandler> logger)
    {
        _gateway = gateway;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentRequested domainEvent, CancellationToken ct = default)
    {
        // 1. Check idempotency
        // 2. Process
        // 3. Publish resulting event
    }
}
```

---

## Idempotency

**Rule:** processing the same event twice must not cause a duplicate effect.

```csharp
public async Task HandleAsync(PaymentRequested domainEvent, CancellationToken ct = default)
{
    var alreadyProcessed = await _paymentRepository
        .ExistsByEventIdAsync(domainEvent.EventId, ct);

    if (alreadyProcessed)
    {
        _logger.LogWarning("Event {EventId} already processed. Skipping.", domainEvent.EventId);
        return;
    }
    // Process normally...
}
```

---

## Payment Flow (Event-Driven)

```
POST /api/v1/payments
        ↓
RequestPaymentCommand
        ↓
RequestPaymentHandler
    - Creates Payment (Status: Pending)
    - Persists via EF Core
    - Publishes PaymentRequested
        ↓
PaymentRequestedHandler
    - Checks idempotency
    - Updates Payment (Status: Processing)
    - Calls MockGatewayService
        ↓ (80% success)              ↓ (20% failure)
PaymentProcessed                 PaymentFailed
        ↓                               ↓
PaymentProcessedHandler          PaymentFailedHandler
    - Payment → Processed            - Payment → Failed
    - Order → Confirmed              - Order → Cancelled
```

---

## Cache Invalidation Flow

```
PUT /admin/catalog/products/{id}
        ↓
UpdateProductCommand
        ↓
UpdateProductHandler
    - Persists via EF Core
    - Publishes ProductUpdated
        ↓
ProductUpdatedHandler (Infrastructure)
    - Removes product key from Redis
    - Removes listing key from Redis
```

---

## InMemoryEventBus (Dev Implementation)

```csharp
public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public async Task PublishAsync<T>(T domainEvent, CancellationToken ct = default)
        where T : IDomainEvent
    {
        _logger.LogInformation(
            "Publishing event {EventType} with id {EventId}",
            typeof(T).Name,
            domainEvent.EventId);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<T>>();

        foreach (var handler in handlers)
            await handler.HandleAsync(domainEvent, ct);
    }
}
```

---

## Where to Publish Events

### ✅ Correct — in the Application Handler
```csharp
// AFTER persisting
await _orderRepository.CreateAsync(order, cancellationToken);

await _eventBus.PublishAsync(new OrderCreated(
    EventId: Guid.NewGuid(),
    OccurredAt: DateTime.UtcNow,
    OrderId: order.Id,
    UserId: order.UserId,
    Total: order.Total
), cancellationToken);
```

### ❌ Wrong — in the API layer
```csharp
// FORBIDDEN
app.MapPost("/orders", async (IEventBus bus) =>
{
    await bus.PublishAsync(new OrderCreated(...));
});
```

### ❌ Wrong — before persisting
```csharp
// FORBIDDEN — publish before saving to database
await _eventBus.PublishAsync(new OrderCreated(...));
await _orderRepository.CreateAsync(order, ct);
```

---

## DI Registration

```csharp
services.AddScoped<IEventHandler<PaymentRequested>, PaymentRequestedHandler>();
services.AddScoped<IEventHandler<PaymentProcessed>, PaymentProcessedHandler>();
services.AddScoped<IEventHandler<PaymentFailed>, PaymentFailedHandler>();
services.AddScoped<IEventHandler<ProductUpdated>, ProductUpdatedHandler>();
services.AddScoped<IEventHandler<OrderCreated>, OrderCreatedHandler>();
services.AddSingleton<IEventBus, InMemoryEventBus>();
```

---

## Checklist for creating a new event

- [ ] Name in the past tense (`[Entity][PastAction]`)
- [ ] Implements `IDomainEvent`
- [ ] Is a `sealed record`
- [ ] Has `EventId (Guid)` and `OccurredAt (DateTime)`
- [ ] Is immutable (init only)
- [ ] Handler created and implements `IEventHandler<T>`
- [ ] Handler registered in DI
- [ ] Handler checks idempotency (if persisting data)
- [ ] Event published **after** persistence
- [ ] Event published in the `Application` layer, never in `API`
- [ ] Unit test for handler created
- [ ] Validation criterion added to the corresponding SPEC

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [DOMAIN-GLOSSARY.md](./DOMAIN-GLOSSARY.md)
- [TECH-STACK.md](./TECH-STACK.md)