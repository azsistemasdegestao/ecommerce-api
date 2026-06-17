using FluentValidation;

namespace Ecommerce.Application.Cart.Commands.AddItemToCart;

public sealed class AddItemToCartValidator : AbstractValidator<AddItemToCartCommand>
{
    public AddItemToCartValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        // BR-CART-003: minimum quantity per item is 1
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
