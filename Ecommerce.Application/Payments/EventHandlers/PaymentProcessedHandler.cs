using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Application.Payments.EventHandlers;

public sealed class PaymentProcessedHandler : IEventHandler<PaymentProcessed>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<PaymentProcessedHandler> _logger;

    public PaymentProcessedHandler(
        IPaymentRepository paymentRepository, IOrderRepository orderRepository, ILogger<PaymentProcessedHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    // BR-PAY-006
    public async Task HandleAsync(PaymentProcessed domainEvent, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByIdAsync(domainEvent.PaymentId, ct);

        // BR-PAY-005: idempotency
        if (payment is null || payment.Status != PaymentStatus.Processing)
        {
            _logger.LogWarning("PaymentProcessed {EventId} already processed. Skipping.", domainEvent.EventId);
            return;
        }

        payment.MarkProcessed();
        _paymentRepository.Update(payment);
        await _paymentRepository.SaveChangesAsync(ct);

        var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
        if (order is not null && order.Status == OrderStatus.Pending)
        {
            order.ChangeStatus(OrderStatus.Confirmed);
            _orderRepository.Update(order);
            await _orderRepository.SaveChangesAsync(ct);
        }
    }
}
