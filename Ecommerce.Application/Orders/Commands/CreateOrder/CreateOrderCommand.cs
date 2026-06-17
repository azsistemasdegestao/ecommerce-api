using MediatR;

namespace Ecommerce.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(Guid UserId, string ShippingAddress) : IRequest<CreateOrderResponse>;
