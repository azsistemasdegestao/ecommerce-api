using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Orders;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetOrderByIdAdmin;

public sealed class GetOrderByIdAdminHandler : IRequestHandler<GetOrderByIdAdminQuery, OrderDetailDto>
{
    private readonly IOrderQueryService _orderQueryService;

    public GetOrderByIdAdminHandler(IOrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    public async Task<OrderDetailDto> Handle(GetOrderByIdAdminQuery request, CancellationToken cancellationToken) =>
        await _orderQueryService.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order not found.");
}
