using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Cart.Commands.RemoveCartItem;

public sealed class RemoveCartItemHandler : IRequestHandler<RemoveCartItemCommand>
{
    private readonly ICartRepository _cartRepository;

    public RemoveCartItemHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByItemIdAsync(request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Cart item not found.");

        // BR-CART-007: a Customer may only manipulate their own Cart
        if (cart.UserId != request.UserId)
            throw new ForbiddenException("This cart item does not belong to you.");

        cart.RemoveItem(request.ItemId);

        _cartRepository.Update(cart);
        await _cartRepository.SaveChangesAsync(cancellationToken);
    }
}
