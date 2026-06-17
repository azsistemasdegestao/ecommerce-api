namespace Ecommerce.Application.Cart.Commands.AddItemToCart;

public sealed record AddItemToCartResponse(
    Guid CartId,
    Guid ItemId,
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
