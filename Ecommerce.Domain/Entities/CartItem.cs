namespace Ecommerce.Domain.Entities;

public sealed class CartItem : BaseEntity
{
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal => Quantity * UnitPrice;

    private CartItem() { }

    public static CartItem Create(Guid cartId, Guid productId, int quantity, decimal unitPrice) => new()
    {
        CartId = cartId,
        ProductId = productId,
        Quantity = quantity,
        UnitPrice = unitPrice
    };

    public void IncreaseQuantity(int amount)
    {
        Quantity += amount;
        UpdateTimestamp();
    }

    public void SetQuantity(int quantity)
    {
        Quantity = quantity;
        UpdateTimestamp();
    }
}
