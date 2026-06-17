using MediatR;

namespace Ecommerce.Application.Cart.Queries.GetCart;

public sealed class GetCartHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly ICartQueryService _cartQueryService;

    public GetCartHandler(ICartQueryService cartQueryService)
    {
        _cartQueryService = cartQueryService;
    }

    // BR-CART-008: never read this through ICacheService — always hit the database directly
    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await _cartQueryService.GetByUserIdAsync(request.UserId, cancellationToken);
        return cart ?? new CartDto(Guid.Empty, Array.Empty<CartItemDto>(), 0, 0, DateTime.UtcNow);
    }
}
