using Ecommerce.Application.Admin;
using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetUsers;

public sealed class GetUsersHandler : IRequestHandler<GetUsersQuery, PagedResponse<UserSummaryDto>>
{
    private readonly IAdminQueryService _adminQueryService;

    public GetUsersHandler(IAdminQueryService adminQueryService)
    {
        _adminQueryService = adminQueryService;
    }

    public Task<PagedResponse<UserSummaryDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;

        return _adminQueryService.GetUsersAsync(pageNumber, pageSize, request.Search, cancellationToken);
    }
}
