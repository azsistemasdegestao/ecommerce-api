using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Cart.Commands.ClearCart;

public sealed class ClearCartHandler : IRequestHandler<ClearCartCommand>
{
    private readonly ICartRepository _cartRepository;

    public ClearCartHandler(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (cart is null)
            return;

        cart.Clear();

        _cartRepository.Update(cart);
        await _cartRepository.SaveChangesAsync(cancellationToken);
    }
}
