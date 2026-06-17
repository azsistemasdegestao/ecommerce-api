using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Payments.Commands.RequestPayment;

public sealed class RequestPaymentHandler : IRequestHandler<RequestPaymentCommand, RequestPaymentResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;

    public RequestPaymentHandler(IOrderRepository orderRepository, IPaymentRepository paymentRepository, IEventBus eventBus)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
    }

    public async Task<RequestPaymentResponse> Handle(RequestPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        // BR-PAY-002
        if (order.UserId != request.UserId)
            throw new UnprocessableEntityException("Order does not belong to Customer.");

        // BR-PAY-001
        if (order.Status != OrderStatus.Pending)
            throw new UnprocessableEntityException("Order is not in Pending status.");

        if (await _paymentRepository.GetByOrderIdAsync(order.Id, cancellationToken) is not null)
            throw new UnprocessableEntityException("A payment has already been requested for this order.");

        // BR-PAY-003
        var payment = Payment.Create(order.Id, order.Total, "MockGateway");
        await _paymentRepository.AddAsync(payment, cancellationToken);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new PaymentRequested(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            PaymentId: payment.Id,
            OrderId: payment.OrderId,
            Amount: payment.Amount), cancellationToken);

        return new RequestPaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.Status.ToString(), "Payment is being processed.");
    }
}
