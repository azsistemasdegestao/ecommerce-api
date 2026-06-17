using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Admin.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusHandler : IRequestHandler<UpdateOrderStatusCommand, UpdateOrderStatusResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;

    public UpdateOrderStatusHandler(IOrderRepository orderRepository, IEventBus eventBus)
    {
        _orderRepository = orderRepository;
        _eventBus = eventBus;
    }

    public async Task<UpdateOrderStatusResponse> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        var newStatus = Enum.Parse<OrderStatus>(request.Status, ignoreCase: true);
        var oldStatus = order.Status;

        // BR-ADMIN-006 / BR-ADMIN-007
        if (!OrderStatusTransitions.CanTransition(oldStatus, newStatus))
            throw new UnprocessableEntityException("Status transition not allowed.");

        order.ChangeStatus(newStatus);

        _orderRepository.Update(order);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // BR-ADMIN-008 / BR-ORD-010
        await _eventBus.PublishAsync(new OrderStatusUpdated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            OrderId: order.Id,
            OldStatus: oldStatus.ToString(),
            NewStatus: order.Status.ToString()), cancellationToken);

        return new UpdateOrderStatusResponse(order.Id, order.Status.ToString(), order.UpdatedAt);
    }
}
