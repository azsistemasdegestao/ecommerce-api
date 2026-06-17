using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetAllOrders;

public sealed record GetAllOrdersQuery(int PageNumber, int PageSize, string? Status, Guid? UserId)
    : IRequest<PagedResponse<AdminOrderSummaryDto>>;
