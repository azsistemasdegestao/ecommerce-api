using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Cart.Commands.UpdateCartItem;

public sealed class UpdateCartItemHandler : IRequestHandler<UpdateCartItemCommand>
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;

    public UpdateCartItemHandler(ICartRepository cartRepository, IProductRepository productRepository)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
    }

    public async Task Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByItemIdAsync(request.ItemId, cancellationToken)
            ?? throw new NotFoundException("Cart item not found.");

        // BR-CART-007: a Customer may only manipulate their own Cart
        if (cart.UserId != request.UserId)
            throw new ForbiddenException("This cart item does not belong to you.");

        var item = cart.FindItem(request.ItemId)!;

        // BR-CART-004
        var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");
        if (request.Quantity > product.Stock)
            throw new UnprocessableEntityException("Insufficient stock.");

        cart.UpdateItemQuantity(request.ItemId, request.Quantity);

        _cartRepository.Update(cart);
        await _cartRepository.SaveChangesAsync(cancellationToken);
    }
}
