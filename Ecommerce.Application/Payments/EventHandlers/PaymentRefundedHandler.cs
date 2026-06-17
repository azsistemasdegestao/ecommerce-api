using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Application.Payments.EventHandlers;

public sealed class PaymentRefundedHandler : IEventHandler<PaymentRefunded>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<PaymentRefundedHandler> _logger;

    public PaymentRefundedHandler(IOrderRepository orderRepository, ILogger<PaymentRefundedHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    // BR-ADMIN-010
    public async Task HandleAsync(PaymentRefunded domainEvent, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetByIdAsync(domainEvent.OrderId, ct);

        // idempotency
        if (order is null || order.Status == OrderStatus.Cancelled)
        {
            _logger.LogWarning("PaymentRefunded {EventId} already processed. Skipping.", domainEvent.EventId);
            return;
        }

        order.ChangeStatus(OrderStatus.Cancelled);
        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(ct);
    }
}
