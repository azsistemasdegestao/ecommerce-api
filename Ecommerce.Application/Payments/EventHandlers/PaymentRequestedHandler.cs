using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Application.Payments.EventHandlers;

public sealed class PaymentRequestedHandler : IEventHandler<PaymentRequested>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IMockGatewayService _gateway;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PaymentRequestedHandler> _logger;

    public PaymentRequestedHandler(
        IPaymentRepository paymentRepository, IMockGatewayService gateway, IEventBus eventBus, ILogger<PaymentRequestedHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _gateway = gateway;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentRequested domainEvent, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(domainEvent.PaymentId, ct);

        // BR-PAY-005: idempotency — only act while the payment is still Pending
        if (payment is null || payment.Status != PaymentStatus.Pending)
        {
            _logger.LogWarning("PaymentRequested {EventId} already processed. Skipping.", domainEvent.EventId);
            return;
        }

        payment.StartProcessing();
        _paymentRepository.Update(payment);
        await _paymentRepository.SaveChangesAsync(ct);

        // BR-PAY-004
        var result = await _gateway.ProcessAsync(payment.Id, payment.Amount, ct);

        if (result.Success)
        {
            await _eventBus.PublishAsync(new PaymentProcessed(
                Guid.NewGuid(), DateTime.UtcNow, payment.Id, payment.OrderId), ct);
        }
        else
        {
            await _eventBus.PublishAsync(new PaymentFailed(
                Guid.NewGuid(), DateTime.UtcNow, payment.Id, payment.OrderId, result.FailureReason ?? "Unknown error"), ct);
        }
    }
}
