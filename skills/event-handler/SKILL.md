# SKILL: event-handler
> Generates domain events and their handlers following project patterns.

---

## Objective

Generate a complete `[Event].cs` + `[Event]Handler.cs` pair ready for implementation,
following the patterns defined in `EVENT-PATTERNS.md`.

---

## Documents this skill reads (required)

```
1. docs/GUARDRAILS.md
2. docs/context/EVENT-PATTERNS.md
3. docs/context/CONVENTIONS.md
4. docs/context/DOMAIN-GLOSSARY.md
5. docs/specs/[feature]/SPEC-[feature].md
6. docs/specs/[feature]/CONTEXT-[feature].md
```

---

## Generation Rules

### 1. Event — required structure

```csharp
/// <summary>
/// Published when [description of what happened].
/// </summary>
public sealed record [Event](
    Guid EventId,
    DateTime OccurredAt,
    // event-specific data
) : IDomainEvent;
```

**Event rules:**
- [ ] Name in past tense (`OrderCreated`, not `CreateOrder`)
- [ ] `sealed record` always
- [ ] `EventId (Guid)` required
- [ ] `OccurredAt (DateTime)` required, always UTC
- [ ] Only necessary data (no complex objects — use IDs and primitives)
- [ ] No business logic

### 2. Handler — required structure

```csharp
public sealed class [Event]Handler : IEventHandler<[Event]>
{
    private readonly I[Dependency]Repository _repository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<[Event]Handler> _logger;

    public async Task HandleAsync([Event] domainEvent, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing {EventType} {EventId}",
            nameof([Event]), domainEvent.EventId);

        // IDEMPOTENCY — required if persisting data
        var entity = await _repository.GetByIdAsync(domainEvent.[EntityId], ct);

        if (entity is null)
        {
            _logger.LogWarning("{EventType} {EventId}: entity not found. Skipping.",
                nameof([Event]), domainEvent.EventId);
            return;
        }

        if (entity.[Status] != [ExpectedStatus])
        {
            _logger.LogWarning("{EventType} {EventId}: already processed. Skipping.",
                nameof([Event]), domainEvent.EventId);
            return;
        }

        // TODO: handler logic
        // entity.Update[State]();
        // await _repository.UpdateAsync(entity, ct);

        // Publish resulting event (if any)
        // await _eventBus.PublishAsync(new [ResultingEvent](...), ct);

        _logger.LogInformation("{EventType} {EventId}: processed successfully.",
            nameof([Event]), domainEvent.EventId);
    }
}
```

### 3. DI Registration — required

```csharp
// Add in Infrastructure/Extensions/InfrastructureExtensions.cs
services.AddScoped<IEventHandler<[Event]>, [Event]Handler>();
```

### 4. Unit test — skeleton

```csharp
public sealed class [Event]HandlerTests
{
    private readonly Mock<I[Dependency]Repository> _repositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly [Event]Handler _handler;

    public [Event]HandlerTests()
    {
        _handler = new [Event]Handler(
            _repositoryMock.Object,
            _eventBusMock.Object,
            Mock.Of<ILogger<[Event]Handler>>());
    }

    [Fact]
    public async Task Should_[Result]_When_[Condition]()
    {
        // Arrange
        var domainEvent = new [Event](Guid.NewGuid(), DateTime.UtcNow /*, ... */);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — TODO
    }

    [Fact]
    public async Task Should_Skip_When_Event_Already_Processed()
    {
        // Arrange — simulate already-processed state
        var domainEvent = new [Event](Guid.NewGuid(), DateTime.UtcNow);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert — repository should not be called for update
        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<[Entity]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

---

## Real project examples

### PaymentRequested + Handler

```csharp
public sealed record PaymentRequested(
    Guid EventId, DateTime OccurredAt,
    Guid PaymentId, Guid OrderId, decimal Amount
) : IDomainEvent;

public sealed class PaymentRequestedHandler : IEventHandler<PaymentRequested>
{
    public async Task HandleAsync(PaymentRequested domainEvent, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByIdAsync(domainEvent.PaymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
        {
            _logger.LogWarning("PaymentRequested {EventId}: already processed. Skipping.", domainEvent.EventId);
            return;
        }

        payment.UpdateStatus(PaymentStatus.Processing);
        await _paymentRepository.UpdateAsync(payment, ct);

        var result = await _gateway.ProcessAsync(payment.Id, payment.Amount, ct);

        if (result.Success)
        {
            payment.UpdateStatus(PaymentStatus.Processed);
            await _paymentRepository.UpdateAsync(payment, ct);
            await _eventBus.PublishAsync(new PaymentProcessed(
                Guid.NewGuid(), DateTime.UtcNow, payment.Id, domainEvent.OrderId), ct);
        }
        else
        {
            payment.UpdateStatus(PaymentStatus.Failed);
            await _paymentRepository.UpdateAsync(payment, ct);
            await _eventBus.PublishAsync(new PaymentFailed(
                Guid.NewGuid(), DateTime.UtcNow, payment.Id, domainEvent.OrderId, result.FailureReason ?? "Unknown"), ct);
        }
    }
}
```

---

## Required checklist

- [ ] Event name in the **past tense**
- [ ] `sealed record` for the event
- [ ] `EventId` and `OccurredAt` present
- [ ] Only primitives and IDs in event data
- [ ] Handler checks **idempotency** before acting
- [ ] Handler logs entry, exit, and skip cases
- [ ] Handler registered in DI
- [ ] Event published **after** persistence (never before)
- [ ] Idempotency test generated (should_skip_when_already_processed)
- [ ] Normal behavior test generated

---

## How to use this skill

```
Prompt:

"Read the following documents in order:
1. docs/GUARDRAILS.md
2. docs/context/EVENT-PATTERNS.md
3. docs/context/CONVENTIONS.md
4. docs/specs/payments/SPEC-payments.md
5. docs/specs/payments/CONTEXT-payments.md

Using the event-handler skill (docs/skills/event-handler/SKILL.md),
generate the PaymentRequested event and its PaymentRequestedHandler."
```