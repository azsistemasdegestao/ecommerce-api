# SPEC-orders.md
> Feature: Orders
> Phase: 4
> Status: Draft

## Context
- [CONTEXT-orders.md](./CONTEXT-orders.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Manages the lifecycle of `Orders`. An Order is created from the Customer's `Cart` (Checkout).
After creation, the Cart is automatically cleared.

---

## Endpoints

### POST /api/v1/orders
Creates an Order from the Customer's active Cart (Checkout).

- **Auth:** JWT required
- **Rate Limit:** `orders` — 20 req/min per UserId

**Request:**
```json
{ "shipping_address": "123 Main St, New York, NY 10001" }
```

**Response 201 Created:**
```json
{
  "id": "uuid",
  "status": "Pending",
  "total": 59.80,
  "shipping_address": "123 Main St, New York, NY 10001",
  "item_count": 2,
  "created_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Shipping address missing |
| 401 | JWT missing or invalid |
| 422 | Empty cart or product out of stock |

---

### GET /api/v1/orders
Lists the authenticated Customer's orders with pagination.

- **Auth:** JWT required

**Query Params:**
```
page_number  int    default: 1
page_size    int    default: 10, max: 50
status       string optional
```

**Response 200 OK:**
```json
{
  "items": [
    {
      "id": "uuid",
      "status": "Confirmed",
      "total": 59.80,
      "item_count": 2,
      "created_at": "2024-01-01T00:00:00Z"
    }
  ],
  "total_count": 10,
  "page_number": 1,
  "page_size": 10,
  "total_pages": 1
}
```

---

### GET /api/v1/orders/{id}
Returns details of a Customer's order.

- **Auth:** JWT required

**Errors:**
| Status | Reason |
|--------|--------|
| 403 | Order does not belong to Customer |
| 404 | Order not found |

---

### POST /api/v1/orders/{id}/cancel
Cancels a Customer's order.

- **Auth:** JWT required

**Response 200 OK:**
```json
{
  "id": "uuid",
  "status": "Cancelled",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 403 | Order does not belong to Customer |
| 422 | Order cannot be cancelled (invalid status) |

---

## Business Rules

- `BR-ORD-001` Cart must have at least one item to create an Order.
- `BR-ORD-002` Validate stock of all items at Checkout time.
- `BR-ORD-003` Order created with status `Pending`.
- `BR-ORD-004` Cart automatically cleared after Order creation.
- `BR-ORD-005` `OrderItems` are snapshots: `ProductName` and `UnitPrice` captured at Checkout.
- `BR-ORD-006` Customer may only view and cancel their own Orders.
- `BR-ORD-007` Cancellation allowed only in `Pending` and `Confirmed` status.
- `BR-ORD-008` Event `OrderCreated` published after creation.
- `BR-ORD-009` Event `OrderCancelled` published after cancellation.
- `BR-ORD-010` Event `OrderStatusUpdated` published after any status change.

---

## Domain Events

| Event | Published when | Effect |
|-------|---------------|--------|
| `OrderCreated` | Order created via Checkout | Triggers payment flow (Phase 5) |
| `OrderCancelled` | Order cancelled | — |
| `OrderStatusUpdated` | Order status changed | — |

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-ORD-U01 | Checkout with valid Cart | Cart with items and stock | Order created, Cart cleared, event published |
| AC-ORD-U02 | Checkout with empty Cart | Cart without items | Business error |
| AC-ORD-U03 | Checkout with out-of-stock product | Product with stock = 0 | Business error |
| AC-ORD-U04 | OrderItem snapshot | Product with price X | OrderItem with UnitPrice = X |
| AC-ORD-U05 | Cancel Pending Order | Order status Pending | Status Cancelled, event published |
| AC-ORD-U06 | Cancel Confirmed Order | Order status Confirmed | Status Cancelled, event published |
| AC-ORD-U07 | Cancel Delivered Order | Order status Delivered | Business error |
| AC-ORD-U08 | Customer cannot access another's Order | Different UserId | Authorization error |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-ORD-I01 | POST /orders with valid Cart | JWT + shipping_address | `201 Created` + Order |
| AC-ORD-I02 | POST /orders with empty Cart | JWT + empty cart | `422 Unprocessable Entity` |
| AC-ORD-I03 | POST /orders without JWT | No token | `401 Unauthorized` |
| AC-ORD-I04 | POST /orders without shipping_address | Body without field | `400 Bad Request` |
| AC-ORD-I05 | GET /orders paginated list | JWT Customer | `200 OK` + list |
| AC-ORD-I06 | GET /orders/{id} own order | JWT + own OrderId | `200 OK` + details |
| AC-ORD-I07 | GET /orders/{id} from another Customer | JWT + other's OrderId | `403 Forbidden` |
| AC-ORD-I08 | GET /orders/{id} non-existing | Invalid OrderId | `404 Not Found` |
| AC-ORD-I09 | POST /orders/{id}/cancel Pending | Pending Order | `200 OK` + Cancelled |
| AC-ORD-I10 | POST /orders/{id}/cancel Delivered | Delivered Order | `422 Unprocessable Entity` |
| AC-ORD-I11 | Cart cleared after Checkout | GET /cart after POST /orders | Empty cart |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| Auth (Phase 1) | Feature | JWT required |
| Cart (Phase 3) | Feature | Source of the Order |
| Catalog (Phase 2) | Feature | Stock validation |
| PostgreSQL | Infrastructure | Persistence |
| Dapper | Infrastructure | Read queries |

## This feature is a dependency of
- Phase 5 (Payments) — Order is the Payment target