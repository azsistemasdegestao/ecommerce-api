using MediatR;

namespace Ecommerce.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid UserId, Guid OrderId) : IRequest<CancelOrderResponse>;
