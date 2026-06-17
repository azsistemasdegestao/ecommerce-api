using MediatR;

namespace Ecommerce.Application.Cart.Commands.ClearCart;

public sealed record ClearCartCommand(Guid UserId) : IRequest;
