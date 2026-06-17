namespace Ecommerce.Application.Cart;

public sealed record CartItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record CartDto(
    Guid Id,
    IReadOnlyList<CartItemDto> Items,
    decimal Total,
    int ItemCount,
    DateTime UpdatedAt);

public interface ICartQueryService
{
    Task<CartDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
