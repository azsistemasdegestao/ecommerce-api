using Ecommerce.Application.Common.Exceptions;
using MediatR;

namespace Ecommerce.Application.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto>
{
    private readonly IOrderQueryService _orderQueryService;

    public GetOrderByIdHandler(IOrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    public async Task<OrderDetailDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderQueryService.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");

        // BR-ORD-006
        if (order.UserId != request.UserId)
            throw new ForbiddenException("This order does not belong to you.");

        return order;
    }
}
