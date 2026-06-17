using MediatR;

namespace Ecommerce.Application.Cart.Queries.GetCart;

public sealed record GetCartQuery(Guid UserId) : IRequest<CartDto>;
