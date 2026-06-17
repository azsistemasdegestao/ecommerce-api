using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetAllPayments;

public sealed class GetAllPaymentsHandler : IRequestHandler<GetAllPaymentsQuery, PagedResponse<AdminPaymentSummaryDto>>
{
    private readonly IAdminQueryService _adminQueryService;

    public GetAllPaymentsHandler(IAdminQueryService adminQueryService)
    {
        _adminQueryService = adminQueryService;
    }

    public Task<PagedResponse<AdminPaymentSummaryDto>> Handle(GetAllPaymentsQuery request, CancellationToken cancellationToken) =>
        _adminQueryService.GetPaymentsAsync(request.PageNumber, request.PageSize, cancellationToken);
}
