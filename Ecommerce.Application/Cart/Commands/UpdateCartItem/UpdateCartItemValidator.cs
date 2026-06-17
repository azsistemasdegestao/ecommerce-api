using FluentValidation;

namespace Ecommerce.Application.Cart.Commands.UpdateCartItem;

public sealed class UpdateCartItemValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        // BR-CART-003: minimum quantity per item is 1
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
