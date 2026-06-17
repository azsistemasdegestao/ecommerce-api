using MediatR;

namespace Ecommerce.Application.Cart.Commands.RemoveCartItem;

public sealed record RemoveCartItemCommand(Guid UserId, Guid ItemId) : IRequest;
