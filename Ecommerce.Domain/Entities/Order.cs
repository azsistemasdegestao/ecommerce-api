using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Domain.Entities;

public sealed class Order : BaseEntity, ISoftDelete
{
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public string ShippingAddress { get; private set; } = string.Empty;
    public DateTime? DeletedAt { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items;

    private Order() { }

    // BR-ORD-003 / BR-ORD-005: created Pending, items are snapshots of name and price at Checkout
    public static Order Create(
        Guid userId, string shippingAddress, IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items)
    {
        var order = new Order
        {
            UserId = userId,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending
        };

        foreach (var item in items)
            order._items.Add(OrderItem.Create(order.Id, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice));

        order.Total = order._items.Sum(i => i.Subtotal);
        return order;
    }

    public void ChangeStatus(OrderStatus newStatus)
    {
        Status = newStatus;
        UpdateTimestamp();
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
        UpdateTimestamp();
    }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;
}
