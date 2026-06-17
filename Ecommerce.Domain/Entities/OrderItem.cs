namespace Ecommerce.Domain.Entities;

public sealed class OrderItem : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal => Quantity * UnitPrice;

    private OrderItem() { }

    public static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice) => new()
    {
        OrderId = orderId,
        ProductId = productId,
        ProductName = productName,
        Quantity = quantity,
        UnitPrice = unitPrice
    };
}
