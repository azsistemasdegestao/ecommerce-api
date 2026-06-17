# ARCHITECTURE.md
> Global context document. Describes the project architecture for use by skills and SPECs.

---

## Overview

The project follows **Clean Architecture** combined with **CQRS** and **Event-Driven** for the payments module.

```
┌─────────────────────────────────────────┐
│                   API                   │  ← HTTP entry, Minimal API, JWT, Rate Limiting
├─────────────────────────────────────────┤
│              Application                │  ← Use cases, Commands, Queries, Validators
├─────────────────────────────────────────┤
│                Domain                   │  ← Entities, Events, Interfaces, Business Rules
├─────────────────────────────────────────┤
│             Infrastructure              │  ← EF Core, Dapper, Redis, EventBus, Identity
└─────────────────────────────────────────┘
```

**Fundamental rule:** dependencies always point inward. Outer layers know the inner ones, never the reverse.

---

## Layers

### Domain
- Core of the system. Zero external dependencies.
- Contains: entities, value objects, repository interfaces, domain events, business rules.
- Does not know EF Core, HTTP, Redis, or any third-party lib.

```
Ecommerce.Domain/
  Entities/
    BaseEntity.cs
    ApplicationUser.cs
    Product.cs
    Category.cs
    Cart.cs
    CartItem.cs
    Order.cs
    OrderItem.cs
    Payment.cs
  Events/
    IDomainEvent.cs
    UserRegistered.cs
    ProductUpdated.cs
    OrderCreated.cs
    PaymentRequested.cs
    PaymentProcessed.cs
    PaymentFailed.cs
  Interfaces/
    IRepository.cs
    IEventBus.cs
    ICacheService.cs
    ITokenService.cs
    IEmailService.cs
  Enums/
    OrderStatus.cs
    PaymentStatus.cs
```

### Application
- Orchestrates use cases using MediatR.
- Contains: Commands, Queries, Handlers, Validators, DTOs.
- Knows only the Domain.
- Does not know EF Core, HTTP, or Redis directly.

```
Ecommerce.Application/
  Auth/
    Commands/
      RegisterUser/
        RegisterUserCommand.cs
        RegisterUserHandler.cs
        RegisterUserValidator.cs
      Login/
      RefreshToken/
      Logout/
      ForgotPassword/
      ResetPassword/
    Queries/
  Admin/
    Commands/
    Queries/
  Catalog/
    Commands/
    Queries/
  Cart/
    Commands/
    Queries/
  Orders/
    Commands/
    Queries/
  Payments/
    Commands/
    EventHandlers/
      PaymentRequestedHandler.cs
      PaymentProcessedHandler.cs
      PaymentFailedHandler.cs
  Common/
    DTOs/
    Helpers/
      SlugHelper.cs
    Behaviors/
      ValidationBehavior.cs
      LoggingBehavior.cs
```

### Infrastructure
- Implements the interfaces defined in Domain.
- Knows EF Core, Dapper, Redis, Identity, etc.
- Never referenced by Application or Domain.

```
Ecommerce.Infrastructure/
  Persistence/
    AppDbContext.cs
    Migrations/
    Repositories/
    Configurations/
  Queries/
    ProductQueryService.cs
    OrderQueryService.cs
    CartQueryService.cs
    AdminQueryService.cs
    PaymentQueryService.cs
  Cache/
    RedisCacheService.cs
    CacheKeys.cs
    Handlers/
      ProductUpdatedCacheHandler.cs
  EventBus/
    InMemoryEventBus.cs
  Auth/
    TokenService.cs
  Email/
    MockEmailService.cs
  Payments/
    MockGatewayService.cs
  Seeding/
    AdminSeeder.cs
  Observability/
    MetricsService.cs
```

### API
- HTTP entry point.
- Contains: Minimal API endpoints, middlewares, DI configuration, Scalar.
- Knows Application and Infrastructure (only for DI registration).
- Never contains business logic.

```
Ecommerce.API/
  Endpoints/
    Auth/
      AuthEndpoints.cs
    Admin/
      AdminUsersEndpoints.cs
      AdminOrdersEndpoints.cs
      AdminPaymentsEndpoints.cs
      AdminCategoriesEndpoints.cs
    Catalog/
      CatalogEndpoints.cs
    Cart/
      CartEndpoints.cs
    Orders/
      OrdersEndpoints.cs
    Payments/
      PaymentsEndpoints.cs
  Middleware/
    ErrorHandlingMiddleware.cs
    RateLimitResponseMiddleware.cs
  Extensions/
    AuthExtensions.cs
    RateLimitingExtensions.cs
    ObservabilityExtensions.cs
    HealthCheckExtensions.cs
  Program.cs
```

---

## CQRS

Every operation is separated into Command (write) or Query (read):

```
Command → MediatR → Handler → EF Core → PostgreSQL
Query   → MediatR → Handler → Dapper  → PostgreSQL
                                ↑
                              Redis (cache)
```

### Commands
- Alter system state.
- Use EF Core via repositories.
- Publish Domain Events after persistence.
- Return only the ID or minimal result.

### Queries
- Never alter state.
- Use Dapper directly via `IDbConnection`.
- Return projected DTOs (never domain entities).
- May use Redis cache (Cache-Aside).

---

## Event-Driven (Payments)

The payments module is asynchronous by design:

```
POST /payments
      ↓
RequestPaymentCommand
      ↓
RequestPaymentHandler
      ↓ publishes
PaymentRequested (event)
      ↓
PaymentRequestedHandler
      ↓ calls
MockGatewayService
      ↓ publishes
PaymentProcessed or PaymentFailed
      ↓
PaymentProcessedHandler → updates Order to Confirmed
PaymentFailedHandler    → updates Order to Cancelled
```

### IEventBus
```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T domainEvent, CancellationToken ct = default)
        where T : IDomainEvent;
}
```

Current implementation: `InMemoryEventBus` (development).
Replaceable by RabbitMQ or Azure Service Bus without changing Domain/Application.

---

## Test Projects

```
Ecommerce.UnitTests/
  Auth/
  Admin/
  Catalog/
  Cart/
  Orders/
  Payments/

Ecommerce.IntegrationTests/
  Auth/
  Admin/
  Catalog/
  Cart/
  Orders/
  Payments/
  Infrastructure/
    CustomWebApplicationFactory.cs
    TestContainersFixture.cs
```

---

## Full Request Flow (example: create order)

```
1. POST /api/v1/orders
   → AuthMiddleware validates JWT
   → RateLimiter checks limit
   → Endpoint handler extracts UserId from token

2. Sends CreateOrderCommand via MediatR
   → ValidationBehavior (FluentValidation) validates the command
   → LoggingBehavior logs input/output

3. CreateOrderHandler executes:
   → Fetches user cart (Dapper)
   → Validates items and stock (Domain)
   → Creates Order entity (Domain)
   → Persists via IOrderRepository (EF Core)
   → Clears cart
   → Publishes OrderCreated (IEventBus)

4. Returns 201 Created with order ID
   → ErrorHandlingMiddleware catches exceptions
   → Serilog logs result
   → OpenTelemetry records trace
```

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [TECH-STACK.md](./TECH-STACK.md)
- [CONVENTIONS.md](./CONVENTIONS.md)
- [EVENT-PATTERNS.md](./EVENT-PATTERNS.md)