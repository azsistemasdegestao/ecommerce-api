# CONVENTIONS.md
> Global context document. Defines naming standards, code structure, and project conventions.
> Skills must follow these conventions when generating any file.

---

## 1. General Naming (C#)

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `OrderHandler`, `ProductRepository` |
| Interfaces | IPascalCase | `IOrderRepository`, `ICacheService` |
| Methods | PascalCase | `CreateOrderAsync`, `GetProductBySlug` |
| Properties | PascalCase | `FirstName`, `CreatedAt` |
| Private fields | _camelCase | `_repository`, `_eventBus` |
| Parameters | camelCase | `orderId`, `userId` |
| Local variables | camelCase | `product`, `totalAmount` |
| Constants | PascalCase | `MaxPageSize`, `DefaultTtl` |
| Enums | PascalCase (type and value) | `OrderStatus.Pending` |
| Records | PascalCase | `CreateOrderCommand`, `LoginResponse` |

---

## 2. Naming by Layer

### Domain — Entities
```csharp
// File: Ecommerce.Domain/Entities/Order.cs
public sealed class Order : BaseEntity
{
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
}
```

### Domain — Events
```csharp
// Pattern: [Entity][PastAction]
// File: Ecommerce.Domain/Events/OrderCreated.cs
public sealed record OrderCreated(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid UserId,
    decimal Total
) : IDomainEvent;
```

### Application — Commands
```csharp
// Pattern: [Action][Entity]Command
public sealed record CreateOrderCommand(
    Guid UserId,
    string ShippingAddress
) : IRequest<CreateOrderResponse>;
```

### Application — Queries
```csharp
// Pattern: Get[Entity/Entities]Query
public sealed record GetOrdersQuery(
    Guid UserId,
    int PageNumber,
    int PageSize
) : IRequest<PagedResponse<OrderSummaryDto>>;
```

### Application — Handlers
```csharp
// Pattern: [Command/Query without suffix]Handler
public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;

    public CreateOrderHandler(IOrderRepository orderRepository, IEventBus eventBus)
    {
        _orderRepository = orderRepository;
        _eventBus = eventBus;
    }

    public async Task<CreateOrderResponse> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        // implementation
    }
}
```

### Application — Validators
```csharp
// Pattern: [Command/Query]Validator
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ShippingAddress).NotEmpty().MaximumLength(500);
    }
}
```

### Application — DTOs
```csharp
// Pattern: [Entity][Context]Dto — always use record
public sealed record OrderSummaryDto(
    Guid Id,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    int ItemCount
);
```

### Infrastructure — Repositories
```csharp
// Pattern: [Entity]Repository
public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _context;
    public OrderRepository(AppDbContext context) => _context = context;
}
```

### Infrastructure — Query Services (Dapper)
```csharp
// Pattern: [Entity]QueryService
public sealed class OrderQueryService : IOrderQueryService
{
    private readonly IDbConnection _connection;
    public OrderQueryService(IDbConnection connection) => _connection = connection;
}
```

### API — Endpoints
```csharp
// Pattern: [Feature]Endpoints
public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Create order from cart")
            .Produces<CreateOrderResponse>(201)
            .Produces<ProblemDetails>(400)
            .Produces<ProblemDetails>(401);
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderCommand command,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Created($"/api/v1/orders/{result.Id}", result);
    }
}
```

---

## 3. File Structure by Feature

```
Application/
  [Feature]/
    Commands/
      [Action][Entity]/
        [Action][Entity]Command.cs
        [Action][Entity]Handler.cs
        [Action][Entity]Validator.cs
        [Action][Entity]Response.cs
    Queries/
      Get[Entity/Entities]/
        Get[Entity/Entities]Query.cs
        Get[Entity/Entities]Handler.cs
    EventHandlers/
      [Event]Handler.cs
```

---

## 4. Database

### Tables (snake_case, plural)
```sql
users, products, categories, carts, cart_items, orders, order_items, payments
```

### Columns (snake_case)
```sql
id          UUID PRIMARY KEY DEFAULT gen_random_uuid()
user_id     UUID NOT NULL
created_at  TIMESTAMP NOT NULL DEFAULT NOW()
updated_at  TIMESTAMP NOT NULL DEFAULT NOW()
deleted_at  TIMESTAMP NULL
```

### Indexes
```sql
-- Naming: idx_[table]_[column]
idx_products_slug
idx_orders_user_id
idx_users_email
```

### Migrations
```
-- Naming: [timestamp]_[Description].cs
20241201_InitialCreate.cs
20241202_AddProductsTable.cs
```

---

## 5. JSON (Request/Response)

All fields in **snake_case**:

```json
// Request
{ "email": "user@example.com", "first_name": "John" }

// Response
{ "id": "uuid", "access_token": "jwt...", "expires_in": 3600 }
```

Global configuration:
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
```

---

## 6. HTTP Responses

### Success
| Operation | Status |
|-----------|--------|
| GET (found) | 200 OK |
| POST (created) | 201 Created + Location header |
| PUT/PATCH | 200 OK |
| DELETE | 204 No Content |
| Async POST (payment) | 202 Accepted |

### Error Status Codes
| Situation | Status |
|-----------|--------|
| Validation failed | 400 Bad Request |
| Not authenticated | 401 Unauthorized |
| No permission | 403 Forbidden |
| Not found | 404 Not Found |
| Conflict (e.g. duplicate email) | 409 Conflict |
| Business rule violated | 422 Unprocessable Entity |
| Rate limit exceeded | 429 Too Many Requests |
| Internal error | 500 Internal Server Error |

---

## 7. Tests

### Naming
```csharp
// Pattern: Should_[Result]_When_[Condition]
Should_Return_401_When_Password_Is_Wrong()
Should_Create_Order_When_Cart_Has_Items()
Should_Publish_PaymentRequested_When_Order_Is_Confirmed()
Should_Invalidate_Cache_When_Product_Is_Updated()
```

### AAA Structure
```csharp
[Fact]
public async Task Should_Return_JWT_When_Login_Is_Valid()
{
    // Arrange
    var command = new LoginCommand("user@test.com", "Password@123");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.AccessToken.Should().NotBeNullOrEmpty();
    result.ExpiresIn.Should().Be(3600);
}
```

---

## 8. Dependency Injection

```csharp
// Each layer registers its own dependencies
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(...);
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IEventBus, InMemoryEventBus>();
    services.AddSingleton<ICacheService, RedisCacheService>();
    return services;
}
```

---

## 9. Async/Await

```csharp
// ✅ Always use CancellationToken in async operations
public async Task<Order> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await _context.Orders.FindAsync([id], ct);

// ✅ Async suffix on async methods
Task<Order> GetByIdAsync(Guid id, CancellationToken ct);

// ❌ Never use .Result or .Wait()
var order = _repository.GetByIdAsync(id).Result; // FORBIDDEN
```

---

## 10. Logs (Serilog)

```csharp
// ✅ Structured logging with named properties
_logger.LogInformation("Order {OrderId} created for user {UserId}", order.Id, userId);
_logger.LogError(ex, "Payment failed for order {OrderId}", orderId);

// ❌ Never use string interpolation in logs
_logger.LogInformation($"Order {order.Id} created"); // FORBIDDEN
```

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [TECH-STACK.md](./TECH-STACK.md)