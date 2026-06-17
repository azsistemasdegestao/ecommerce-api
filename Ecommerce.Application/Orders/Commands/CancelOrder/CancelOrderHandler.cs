using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Orders.Commands.CancelOrder;

public sealed class CancelOrderHandler : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;

    public CancelOrderHandler(IOrderRepository orderRepository, IEventBus eventBus)
    {
        _orderRepository = orderRepository;
        _eventBus = eventBus;
    }

    public async Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        // BR-ORD-006
        if (order.UserId != request.UserId)
            throw new ForbiddenException("This order does not belong to you.");

        // BR-ORD-007
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            throw new UnprocessableEntityException("Order cannot be cancelled in its current status.");

        order.Cancel();

        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // BR-ORD-009
        await _eventBus.PublishAsync(new OrderCancelled(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            OrderId: order.Id), cancellationToken);

        return new CancelOrderResponse(order.Id, order.Status.ToString(), order.UpdatedAt);
    }
}
