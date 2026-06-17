using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Orders.Queries.GetOrders;

public sealed class GetOrdersHandler : IRequestHandler<GetOrdersQuery, PagedResponse<OrderSummaryDto>>
{
    private readonly IOrderQueryService _orderQueryService;

    public GetOrdersHandler(IOrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    // BR: order data is never cached — read directly from the database every time
    public Task<PagedResponse<OrderSummaryDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken) =>
        _orderQueryService.GetOrdersAsync(request.UserId, request.PageNumber, request.PageSize, request.Status, cancellationToken);
}
