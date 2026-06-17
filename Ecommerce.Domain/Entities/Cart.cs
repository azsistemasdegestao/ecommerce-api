namespace Ecommerce.Domain.Entities;

public sealed class Cart : BaseEntity
{
    public Guid UserId { get; private set; }

    private readonly List<CartItem> _items = new();
    public IReadOnlyCollection<CartItem> Items => _items;

    public decimal Total => _items.Sum(i => i.Subtotal);
    public int ItemCount => _items.Count;

    private Cart() { }

    public static Cart Create(Guid userId) => new() { UserId = userId };

    // BR-CART-002: increment quantity if the product is already in the cart
    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
            existing.IncreaseQuantity(quantity);
        else
            _items.Add(CartItem.Create(Id, productId, quantity, unitPrice));

        UpdateTimestamp();
    }

    public CartItem? FindItem(Guid itemId) => _items.FirstOrDefault(i => i.Id == itemId);

    public void UpdateItemQuantity(Guid itemId, int quantity)
    {
        var item = FindItem(itemId) ?? throw new InvalidOperationException("Item not found in cart.");
        item.SetQuantity(quantity);
        UpdateTimestamp();
    }

    public void RemoveItem(Guid itemId)
    {
        _items.RemoveAll(i => i.Id == itemId);
        UpdateTimestamp();
    }

    public void Clear()
    {
        _items.Clear();
        UpdateTimestamp();
    }
}
