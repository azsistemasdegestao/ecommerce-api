using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Interfaces;
using MediatR;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.Application.Cart.Commands.AddItemToCart;

public sealed class AddItemToCartHandler : IRequestHandler<AddItemToCartCommand, AddItemToCartResponse>
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;

    public AddItemToCartHandler(ICartRepository cartRepository, IProductRepository productRepository)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
    }

    public async Task<AddItemToCartResponse> Handle(AddItemToCartCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        // BR-CART-004: validate stock against the resulting quantity (existing + requested)
        var existingQuantity = cart?.Items.FirstOrDefault(i => i.ProductId == product.Id)?.Quantity ?? 0;
        if (existingQuantity + request.Quantity > product.Stock)
            throw new UnprocessableEntityException("Insufficient stock.");

        // BR-CART-001: each Customer has at most one active Cart
        var isNewCart = cart is null;
        cart ??= DomainCart.Create(request.UserId);

        // BR-CART-002 / BR-CART-005: increment quantity if already in cart, snapshot price otherwise
        cart.AddItem(product.Id, request.Quantity, product.Price);

        if (isNewCart)
            await _cartRepository.AddAsync(cart, cancellationToken);
        else
            _cartRepository.Update(cart);

        await _cartRepository.SaveChangesAsync(cancellationToken);

        var item = cart.Items.First(i => i.ProductId == product.Id);
        return new AddItemToCartResponse(cart.Id, item.Id, item.ProductId, item.Quantity, item.UnitPrice, item.Subtotal);
    }
}
