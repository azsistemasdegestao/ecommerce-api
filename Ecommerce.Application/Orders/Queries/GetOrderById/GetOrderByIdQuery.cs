using MediatR;

namespace Ecommerce.Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid UserId, Guid OrderId) : IRequest<OrderDetailDto>;
