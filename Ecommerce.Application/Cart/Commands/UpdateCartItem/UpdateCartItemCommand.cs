using MediatR;

namespace Ecommerce.Application.Cart.Commands.UpdateCartItem;

public sealed record UpdateCartItemCommand(Guid UserId, Guid ItemId, int Quantity) : IRequest;
