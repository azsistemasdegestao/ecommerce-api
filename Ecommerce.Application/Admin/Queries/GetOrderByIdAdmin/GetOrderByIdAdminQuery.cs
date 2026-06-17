using Ecommerce.Application.Orders;
using MediatR;

namespace Ecommerce.Application.Admin.Queries.GetOrderByIdAdmin;

public sealed record GetOrderByIdAdminQuery(Guid OrderId) : IRequest<OrderDetailDto>;
