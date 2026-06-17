# CONTEXT-payments.md
> Feature-specific context document for Payments.

---

## Data Model

```csharp
public sealed class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; }
}

public enum PaymentStatus
{
    Pending, Processing, Processed, Failed, Refunded
}
```

---

## Events

```csharp
public sealed record PaymentRequested(
    Guid EventId, DateTime OccurredAt,
    Guid PaymentId, Guid OrderId, decimal Amount
) : IDomainEvent;

public sealed record PaymentProcessed(
    Guid EventId, DateTime OccurredAt,
    Guid PaymentId, Guid OrderId
) : IDomainEvent;

public sealed record PaymentFailed(
    Guid EventId, DateTime OccurredAt,
    Guid PaymentId, Guid OrderId, string Reason
) : IDomainEvent;

public sealed record PaymentRefunded(
    Guid EventId, DateTime OccurredAt,
    Guid PaymentId, Guid OrderId, decimal Amount
) : IDomainEvent;
```

---

## MockGatewayService

```csharp
public interface IMockGatewayService
{
    Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, CancellationToken ct);
}

public sealed record GatewayResult(bool Success, string? FailureReason);

public sealed class MockGatewayService : IMockGatewayService
{
    private readonly Random _random = new();

    public async Task<GatewayResult> ProcessAsync(
        Guid paymentId, decimal amount, CancellationToken ct)
    {
        var delay = _random.Next(100, 500);
        await Task.Delay(delay, ct);

        var success = _random.NextDouble() > 0.20;
        return success
            ? new GatewayResult(true, null)
            : new GatewayResult(false, "Insufficient funds");
    }
}
```

---

## Idempotency

```csharp
public sealed class PaymentRequestedHandler : IEventHandler<PaymentRequested>
{
    public async Task HandleAsync(PaymentRequested domainEvent, CancellationToken ct)
    {
        var payment = await _paymentRepository.GetByIdAsync(domainEvent.PaymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
        {
            _logger.LogWarning("PaymentRequested {EventId} already processed. Skipping.", domainEvent.EventId);
            return;
        }
        // ... process
    }
}
```

---

## File Structure

```
Ecommerce.Domain/
  Entities/Payment.cs
  Enums/PaymentStatus.cs
  Events/PaymentRequested.cs / PaymentProcessed.cs / PaymentFailed.cs / PaymentRefunded.cs
  Interfaces/IPaymentRepository.cs / IMockGatewayService.cs

Ecommerce.Application/
  Payments/
    Commands/RequestPayment/
    EventHandlers/
      PaymentRequestedHandler.cs / PaymentProcessedHandler.cs
      PaymentFailedHandler.cs / PaymentRefundedHandler.cs
    Queries/GetPaymentByOrderId/

Ecommerce.Infrastructure/
  Persistence/Repositories/PaymentRepository.cs
  Persistence/Configurations/PaymentConfiguration.cs
  Payments/MockGatewayService.cs
  Queries/PaymentQueryService.cs

Ecommerce.API/Endpoints/Payments/PaymentsEndpoints.cs

Ecommerce.UnitTests/Payments/
  RequestPaymentHandlerTests.cs / PaymentRequestedHandlerTests.cs
  PaymentProcessedHandlerTests.cs / PaymentFailedHandlerTests.cs
  MockGatewayServiceTests.cs / IdempotencyTests.cs

Ecommerce.IntegrationTests/Payments/PaymentsEndpointsTests.cs
```

---

## References
- [SPEC-payments.md](./SPEC-payments.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)