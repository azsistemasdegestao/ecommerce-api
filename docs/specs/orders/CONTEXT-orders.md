# CONTEXT-orders.md
> Feature-specific context document for Orders.

---

## Data Model

```csharp
public sealed class Order : BaseEntity, ISoftDelete
{
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    public string ShippingAddress { get; private set; }
    public ICollection<OrderItem> Items { get; private set; }
    public DateTime? DeletedAt { get; private set; }
}

public sealed class OrderItem : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }  // snapshot
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }   // snapshot
    public decimal Subtotal => Quantity * UnitPrice;
}

public enum OrderStatus
{
    Pending, Confirmed, Processing, Shipped, Delivered, Cancelled
}
```

---

## Allowed Status Transitions

```csharp
private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
{
    [OrderStatus.Pending]    = [OrderStatus.Confirmed, OrderStatus.Cancelled],
    [OrderStatus.Confirmed]  = [OrderStatus.Processing, OrderStatus.Cancelled],
    [OrderStatus.Processing] = [OrderStatus.Shipped],
    [OrderStatus.Shipped]    = [OrderStatus.Delivered],
    [OrderStatus.Delivered]  = [],
    [OrderStatus.Cancelled]  = []
};
```

---

## Dapper Queries

```sql
-- GetOrdersQuery (Customer)
SELECT o.id, o.status, o.total, o.created_at,
       COUNT(oi.id) as item_count, COUNT(*) OVER() as total_count
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
WHERE o.user_id = @UserId AND o.deleted_at IS NULL
  AND (@Status IS NULL OR o.status = @Status)
GROUP BY o.id
ORDER BY o.created_at DESC
LIMIT @PageSize OFFSET @Offset;

-- GetOrderByIdQuery
SELECT o.id, o.status, o.total, o.shipping_address, o.created_at, o.updated_at,
       oi.id as item_id, oi.product_id, oi.product_name, oi.quantity, oi.unit_price
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
WHERE o.id = @OrderId AND o.deleted_at IS NULL;
```

---

## File Structure

```
Ecommerce.Domain/
  Entities/Order.cs / OrderItem.cs
  Enums/OrderStatus.cs
  Events/OrderCreated.cs / OrderCancelled.cs / OrderStatusUpdated.cs
  Interfaces/IOrderRepository.cs

Ecommerce.Application/
  Orders/
    Commands/CreateOrder/ CancelOrder/
    Queries/GetOrders/ GetOrderById/

Ecommerce.Infrastructure/
  Persistence/Repositories/OrderRepository.cs
  Persistence/Configurations/OrderConfiguration.cs / OrderItemConfiguration.cs
  Queries/OrderQueryService.cs

Ecommerce.API/Endpoints/Orders/OrdersEndpoints.cs

Ecommerce.UnitTests/Orders/
  CreateOrderHandlerTests.cs / CancelOrderHandlerTests.cs
  OrderStatusTransitionTests.cs

Ecommerce.IntegrationTests/Orders/OrdersEndpointsTests.cs
```

---

## References
- [SPEC-orders.md](./SPEC-orders.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)