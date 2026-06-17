# DOMAIN-GLOSSARY.md
> Global context document. Defines the ubiquitous language of the domain.
> All code, SPECs, and skills must use exactly these terms — no translations or synonyms.

---

## Usage Rule

> If a term exists in this glossary, it **must** be used exactly as defined.
> Never use synonyms like `client` for `Customer`, or `bill` for `Payment`.

---

## Domain Terms

### User
A person registered in the system. May have role `Customer` or `Admin`.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `Email` | string | Login, unique in the system |
| `FirstName` | string | First name |
| `LastName` | string | Last name |
| `Role` | enum | `Customer` or `Admin` |
| `CreatedAt` | DateTime | Registration date |
| `DeletedAt` | DateTime? | Soft delete |

**Related terms:**
- `Register` → act of creating a new User
- `Login` → act of authenticating an existing User
- `Logout` → act of invalidating the session
- `Lockout` → temporary block after failed login attempts

---

### Customer
A `User` with role `Customer`. Can browse the catalog, manage their cart, and place orders.

> ⚠️ Not to be confused with `User`. Every `Customer` is a `User`, but not every `User` is a `Customer` (could be `Admin`).

---

### Admin
A `User` with role `Admin`. Has full system access, including product, order, and user management.

---

### Product
An item available for sale in the catalog.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `Name` | string | Product name |
| `Description` | string | Detailed description |
| `Slug` | string | URL-friendly name (e.g. `blue-t-shirt-m`) |
| `Price` | decimal | Unit price |
| `Stock` | int | Available quantity |
| `ImageUrl` | string | Main image URL |
| `CategoryId` | UUID | Product category |
| `CreatedAt` | DateTime | Registration date |
| `UpdatedAt` | DateTime | Last update |
| `DeletedAt` | DateTime? | Soft delete |

**Related terms:**
- `InStock` → product with `Stock > 0`
- `OutOfStock` → product with `Stock = 0`
- `Catalog` → set of all available Products (not deleted)

---

### Category
A grouping of Products by type.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `Name` | string | Category name |
| `Slug` | string | URL-friendly name (e.g. `t-shirts`) |

---

### Cart
A temporary collection of Products selected by a Customer before completing a purchase.
Each Customer has at most **one active Cart** at a time.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `UserId` | UUID | Customer who owns the cart |
| `Items` | CartItem[] | List of items |
| `Total` | decimal | Calculated sum of all items |
| `CreatedAt` | DateTime | Creation date |
| `UpdatedAt` | DateTime | Last modification |

**Related terms:**
- `AddItem` → add a Product to the Cart
- `RemoveItem` → remove a CartItem from the Cart
- `ClearCart` → remove all CartItems
- `Checkout` → act of converting the Cart into an Order

---

### CartItem
A Product inside a Cart, with quantity and price at the time of addition.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `CartId` | UUID | Cart it belongs to |
| `ProductId` | UUID | Referenced Product |
| `Quantity` | int | Quantity (minimum: 1) |
| `UnitPrice` | decimal | Unit price at time of addition |
| `Subtotal` | decimal | `Quantity × UnitPrice` (calculated) |

> ⚠️ `UnitPrice` is captured at addition time. Changes in the Product price do not affect existing CartItems.

---

### Order
The result of a Cart `Checkout`. Represents the confirmed purchase intent of a Customer.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `UserId` | UUID | Customer who placed the order |
| `Status` | OrderStatus | Current state of the order |
| `Total` | decimal | Total order value |
| `ShippingAddress` | string | Delivery address |
| `CreatedAt` | DateTime | Order date |
| `UpdatedAt` | DateTime | Last update |
| `DeletedAt` | DateTime? | Soft delete |

---

### OrderStatus
Lifecycle of an Order.

```
Pending → Confirmed → Processing → Shipped → Delivered
    ↓
Cancelled
```

| Value | Description | Allowed transitions |
|-------|-------------|---------------------|
| `Pending` | Order created, awaiting payment | → `Confirmed`, `Cancelled` |
| `Confirmed` | Payment approved | → `Processing`, `Cancelled` |
| `Processing` | Order being prepared | → `Shipped` |
| `Shipped` | Order dispatched | → `Delivered` |
| `Delivered` | Order delivered | — (final) |
| `Cancelled` | Order cancelled | — (final) |

---

### OrderItem
A snapshot of a CartItem at the time of Checkout. Immutable after creation.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `OrderId` | UUID | Order it belongs to |
| `ProductId` | UUID | Referenced Product |
| `ProductName` | string | Product name at order time |
| `Quantity` | int | Quantity |
| `UnitPrice` | decimal | Unit price at order time |
| `Subtotal` | decimal | `Quantity × UnitPrice` (calculated) |

> ⚠️ `ProductName` and `UnitPrice` are snapshots — future Product changes do not affect OrderItems.

---

### Payment
Represents a payment attempt for an Order.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | UUID | Unique identifier |
| `OrderId` | UUID | Associated Order |
| `Amount` | decimal | Charged amount |
| `Status` | PaymentStatus | Current state |
| `Provider` | string | Gateway used (e.g. `MockGateway`) |
| `CreatedAt` | DateTime | Attempt date |
| `UpdatedAt` | DateTime | Last update |

---

### PaymentStatus
Lifecycle of a Payment.

```
Pending → Processing → Processed
                   ↓
                 Failed
```

| Value | Description |
|-------|-------------|
| `Pending` | Payment requested, awaiting processing |
| `Processing` | Being processed by the gateway |
| `Processed` | Successfully approved |
| `Failed` | Rejected or gateway error |
| `Refunded` | Refunded by Admin |

---

### Gateway
External service responsible for processing Payments. Currently mocked (`MockGateway`).

**MockGateway:**
- Simulates approval in **80%** of cases → publishes `PaymentProcessed`
- Simulates failure in **20%** of cases → publishes `PaymentFailed`
- Simulated delay: **100–500ms**

---

## Domain Events

| Event | Published when | Effect |
|-------|---------------|--------|
| `UserRegistered` | New User registered | — |
| `UserLoggedIn` | Successful login | — |
| `UserRoleAssigned` | Role assigned to User | — |
| `ProductCreated` | New Product registered | — |
| `ProductUpdated` | Product changed | Invalidates cache |
| `ProductDeleted` | Product removed | Invalidates cache |
| `OrderCreated` | Cart converted to Order | — |
| `OrderCancelled` | Order cancelled | — |
| `OrderStatusUpdated` | Order status changed | — |
| `PaymentRequested` | Payment requested | Triggers processing |
| `PaymentProcessed` | Payment approved | Order → `Confirmed` |
| `PaymentFailed` | Payment rejected | Order → `Cancelled` |
| `PaymentRefunded` | Payment refunded by Admin | Order → `Cancelled` |

---

## Main Flow (Happy Path)

```
Customer registers
    ↓
Customer logs in → receives JWT
    ↓
Customer browses the Catalog
    ↓
Customer adds Products to Cart
    ↓
Customer checks out → Cart becomes Order (Status: Pending)
    ↓
System requests Payment → PaymentRequested
    ↓
MockGateway approves → PaymentProcessed
    ↓
Order → Status: Confirmed
```

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [CONVENTIONS.md](./CONVENTIONS.md)