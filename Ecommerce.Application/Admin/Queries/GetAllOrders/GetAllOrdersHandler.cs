using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetAllOrders;

public sealed class GetAllOrdersHandler : IRequestHandler<GetAllOrdersQuery, PagedResponse<AdminOrderSummaryDto>>
{
    private readonly IAdminQueryService _adminQueryService;

    public GetAllOrdersHandler(IAdminQueryService adminQueryService)
    {
        _adminQueryService = adminQueryService;
    }

    public Task<PagedResponse<AdminOrderSummaryDto>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken) =>
        _adminQueryService.GetOrdersAsync(request.PageNumber, request.PageSize, request.Status, request.UserId, cancellationToken);
}
