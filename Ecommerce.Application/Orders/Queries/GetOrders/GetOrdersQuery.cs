using Ecommerce.Application.Common.DTOs;
using MediatR;

namespace Ecommerce.Application.Orders.Queries.GetOrders;

public sealed record GetOrdersQuery(Guid UserId, int PageNumber, int PageSize, string? Status)
    : IRequest<PagedResponse<OrderSummaryDto>>;
