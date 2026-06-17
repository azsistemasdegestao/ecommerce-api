# SPEC-cart.md
> Feature: Cart
> Phase: 3
> Status: Draft

## Context
- [CONTEXT-cart.md](./CONTEXT-cart.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Manages the `Customer`'s `Cart`. Each Customer has at most one active Cart.
All routes require JWT.

---

## Endpoints

### GET /api/v1/cart
Returns the active Cart of the authenticated Customer.

- **Auth:** JWT required
- **Rate Limit:** `user` — 60 req/min per UserId

**Response 200 OK:**
```json
{
  "id": "uuid",
  "items": [
    {
      "id": "uuid",
      "product_id": "uuid",
      "product_name": "Blue T-Shirt M",
      "product_slug": "blue-t-shirt-m",
      "image_url": "https://...",
      "unit_price": 29.90,
      "quantity": 2,
      "subtotal": 59.80
    }
  ],
  "total": 59.80,
  "item_count": 2,
  "updated_at": "2024-01-01T00:00:00Z"
}
```

> Returns `200` with empty cart if no active Cart exists.

---

### POST /api/v1/cart/items
Adds a product to the Cart.

- **Auth:** JWT required

**Request:**
```json
{ "product_id": "uuid", "quantity": 2 }
```

**Response 201 Created**

**Errors:**
| Status | Reason |
|--------|--------|
| 404 | Product not found |
| 422 | Insufficient stock |

---

### PUT /api/v1/cart/items/{itemId}
Updates the quantity of a CartItem.

- **Auth:** JWT required

**Request:**
```json
{ "quantity": 3 }
```

**Errors:**
| Status | Reason |
|--------|--------|
| 403 | CartItem does not belong to user |
| 422 | Insufficient stock for new quantity |

---

### DELETE /api/v1/cart/items/{itemId}
Removes a CartItem from the Cart.

- **Auth:** JWT required

**Response 204 No Content**

---

### DELETE /api/v1/cart
Clears all items from the Cart.

- **Auth:** JWT required

**Response 204 No Content**

---

## Business Rules

- `BR-CART-001` Each Customer has at most **one active Cart** at a time.
- `BR-CART-002` If the product already exists in Cart, increment quantity (no duplicate item).
- `BR-CART-003` Minimum quantity per item: **1**.
- `BR-CART-004` Validate available stock when adding or updating quantity.
- `BR-CART-005` `UnitPrice` captured at addition time — not updated if price changes.
- `BR-CART-006` `Total` always calculated dynamically (sum of subtotals).
- `BR-CART-007` Customer may only manipulate their own Cart.
- `BR-CART-008` Cart must not be cached in Redis.
- `BR-CART-009` Cart is automatically cleared after Order creation (Checkout).

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-CART-U01 | Add new product to empty cart | Valid ProductId + quantity | CartItem created, Cart created |
| AC-CART-U02 | Add product already in cart | ProductId already in Cart | Quantity incremented |
| AC-CART-U03 | Add with zero quantity | `quantity: 0` | Validation error |
| AC-CART-U04 | Add out-of-stock product | Stock = 0 | Business error |
| AC-CART-U05 | Add quantity greater than stock | Quantity > Stock | Business error |
| AC-CART-U06 | Update valid quantity | Valid ItemId + quantity | CartItem updated |
| AC-CART-U07 | Update with quantity greater than stock | Quantity > Stock | Business error |
| AC-CART-U08 | Remove existing item | Valid ItemId | CartItem removed |
| AC-CART-U09 | Clear cart | Cart with items | All CartItems removed |
| AC-CART-U10 | Total correctly calculated | 2 items with different prices | Total = sum of subtotals |
| AC-CART-U11 | Customer cannot access another's Cart | Different UserId | Authorization error |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-CART-I01 | GET /cart no items | Empty cart | `200 OK` + empty list |
| AC-CART-I02 | GET /cart with items | Cart with products | `200 OK` + items + total |
| AC-CART-I03 | GET /cart without JWT | No token | `401 Unauthorized` |
| AC-CART-I04 | POST /cart/items valid product | ProductId + quantity | `201 Created` + CartItem |
| AC-CART-I05 | POST /cart/items non-existing product | Invalid ProductId | `404 Not Found` |
| AC-CART-I06 | POST /cart/items out of stock | Stock = 0 | `422 Unprocessable Entity` |
| AC-CART-I07 | POST /cart/items duplicate product | Same ProductId | `201 Created` + summed quantity |
| AC-CART-I08 | PUT /cart/items/{id} valid quantity | Valid quantity | `200 OK` |
| AC-CART-I09 | PUT /cart/items/{id} no stock | Quantity > Stock | `422 Unprocessable Entity` |
| AC-CART-I10 | PUT /cart/items/{id} from another user | ItemId from another Cart | `403 Forbidden` |
| AC-CART-I11 | DELETE /cart/items/{id} existing | Valid ItemId | `204 No Content` |
| AC-CART-I12 | DELETE /cart/items/{id} from another user | ItemId from another Cart | `403 Forbidden` |
| AC-CART-I13 | DELETE /cart clear cart | Cart with items | `204 No Content` |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| Auth (Phase 1) | Feature | JWT required |
| Catalog (Phase 2) | Feature | Validates product and stock |
| PostgreSQL | Infrastructure | Persistence |
| Dapper | Infrastructure | Cart read query |

## This feature is a dependency of
- Phase 4 (Orders) — Cart is converted to Order at Checkout