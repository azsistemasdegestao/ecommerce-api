using MediatR;

namespace Ecommerce.Application.Cart.Commands.AddItemToCart;

public sealed record AddItemToCartCommand(Guid UserId, Guid ProductId, int Quantity) : IRequest<AddItemToCartResponse>;
