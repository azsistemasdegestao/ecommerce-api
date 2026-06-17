# CONTEXT-cart.md
> Feature-specific context document for Cart.

---

## Data Model

```csharp
public sealed class Cart : BaseEntity
{
    public Guid UserId { get; private set; }
    public ICollection<CartItem> Items { get; private set; } = new List<CartItem>();
    public decimal Total => Items.Sum(i => i.Subtotal);
    public int ItemCount => Items.Count;
}

public sealed class CartItem : BaseEntity
{
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal => Quantity * UnitPrice;
}
```

---

## Dapper Query

```sql
SELECT c.id, c.user_id, c.updated_at,
       ci.id as item_id, ci.product_id, ci.quantity, ci.unit_price,
       p.name as product_name, p.slug as product_slug, p.image_url
FROM carts c
LEFT JOIN cart_items ci ON ci.cart_id = c.id
LEFT JOIN products p ON p.id = ci.product_id
WHERE c.user_id = @UserId;
```

---

## File Structure

```
Ecommerce.Domain/
  Entities/Cart.cs / CartItem.cs
  Interfaces/ICartRepository.cs

Ecommerce.Application/
  Cart/
    Commands/AddItemToCart/ UpdateCartItem/ RemoveCartItem/ ClearCart/
    Queries/GetCart/

Ecommerce.Infrastructure/
  Persistence/Repositories/CartRepository.cs
  Persistence/Configurations/CartConfiguration.cs / CartItemConfiguration.cs
  Queries/CartQueryService.cs

Ecommerce.API/Endpoints/Cart/CartEndpoints.cs

Ecommerce.UnitTests/Cart/
  AddItemToCartHandlerTests.cs / UpdateCartItemHandlerTests.cs
  RemoveCartItemHandlerTests.cs / CartTotalCalculationTests.cs

Ecommerce.IntegrationTests/Cart/CartEndpointsTests.cs
```

---

## References
- [SPEC-cart.md](./SPEC-cart.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)