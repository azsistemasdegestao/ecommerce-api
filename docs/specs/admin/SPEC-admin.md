# SPEC-admin.md
> Feature: Administration
> Phase: 1.5
> Status: Draft

## Context
- [CONTEXT-admin.md](./CONTEXT-admin.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Provides endpoints exclusively for `Admin` role users to manage the system.
All routes require JWT with role `Admin` — return `403` for `Customer` and `401` without JWT.

Responsibilities:
- User management (list, deactivate, unlock, assign roles)
- Order management (list all, force status)
- Payment management (list all, refund)
- Category management (create, edit, delete)

---

## Endpoints

### User Management

#### GET /api/v1/admin/users
Lists all system users with pagination.

- **Auth:** JWT + Role `Admin`
- **Rate Limit:** `user` — 60 req/min per UserId

**Query Params:**
```
page_number  int     default: 1
page_size    int     default: 20, max: 100
search       string  optional — filter by name or email
```

**Response 200 OK:**
```json
{
  "items": [
    {
      "id": "uuid",
      "first_name": "John",
      "last_name": "Doe",
      "email": "john@example.com",
      "role": "Customer",
      "is_locked": false,
      "created_at": "2024-01-01T00:00:00Z",
      "deleted_at": null
    }
  ],
  "total_count": 100,
  "page_number": 1,
  "page_size": 20,
  "total_pages": 5
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 429 | Rate limit exceeded |

---

#### GET /api/v1/admin/users/{id}
Returns details of a specific user.

- **Auth:** JWT + Role `Admin`

**Response 200 OK:**
```json
{
  "id": "uuid",
  "first_name": "John",
  "last_name": "Doe",
  "email": "john@example.com",
  "role": "Customer",
  "is_locked": false,
  "lockout_end": null,
  "failed_login_attempts": 0,
  "created_at": "2024-01-01T00:00:00Z",
  "updated_at": "2024-01-01T00:00:00Z",
  "deleted_at": null
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 404 | User not found |

---

#### DELETE /api/v1/admin/users/{id}
Deactivates a user (soft delete).

- **Auth:** JWT + Role `Admin`

**Response 204 No Content**

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Admin trying to deactivate themselves |
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 404 | User not found |

---

#### POST /api/v1/admin/users/{id}/unlock
Removes the lockout from a blocked user.

- **Auth:** JWT + Role `Admin`

**Response 200 OK:**
```json
{ "message": "User successfully unlocked." }
```

**Errors:**
| Status | Reason |
|--------|--------|
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 404 | User not found |
| 422 | User is not locked |

---

#### POST /api/v1/admin/users/{id}/roles
Assigns or changes a user's role.

- **Auth:** JWT + Role `Admin`

**Request:**
```json
{ "role": "Admin" }
```

**Response 200 OK:**
```json
{ "message": "Role successfully assigned." }
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Invalid role |
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 404 | User not found |

---

### Order Management

#### GET /api/v1/admin/orders
Lists all system orders with pagination.

- **Auth:** JWT + Role `Admin`

**Query Params:**
```
page_number  int     default: 1
page_size    int     default: 20, max: 100
status       string  optional
user_id      uuid    optional
```

**Response 200 OK:**
```json
{
  "items": [
    {
      "id": "uuid",
      "user_id": "uuid",
      "user_email": "john@example.com",
      "status": "Pending",
      "total": 199.90,
      "item_count": 3,
      "created_at": "2024-01-01T00:00:00Z"
    }
  ],
  "total_count": 500,
  "page_number": 1,
  "page_size": 20,
  "total_pages": 25
}
```

---

#### POST /api/v1/admin/orders/{id}/status
Forces a status update on an order.

- **Auth:** JWT + Role `Admin`

**Request:**
```json
{ "status": "Shipped" }
```

**Response 200 OK:**
```json
{
  "id": "uuid",
  "status": "Shipped",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Invalid status |
| 401 | JWT missing or invalid |
| 403 | User is not Admin |
| 404 | Order not found |
| 422 | Status transition not allowed |

---

### Payment Management

#### GET /api/v1/admin/payments
Lists all system payments with pagination.

- **Auth:** JWT + Role `Admin`

#### POST /api/v1/admin/payments/{id}/refund
Refunds an approved payment.

- **Auth:** JWT + Role `Admin`

**Response 200 OK:**
```json
{
  "id": "uuid",
  "status": "Refunded",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 422 | Payment is not in Processed status |

---

### Category Management

#### POST /api/v1/admin/categories
Creates a new category.

- **Auth:** JWT + Role `Admin`

**Request:**
```json
{ "name": "T-Shirts", "slug": "t-shirts" }
```

**Response 201 Created:**
```json
{ "id": "uuid", "name": "T-Shirts", "slug": "t-shirts" }
```

#### PUT /api/v1/admin/categories/{id}
Updates an existing category.

#### DELETE /api/v1/admin/categories/{id}
Removes a category (soft delete).

**Errors:**
| Status | Reason |
|--------|--------|
| 422 | Category has active products |

---

## Business Rules

### Users
- `BR-ADMIN-001` Admin cannot deactivate themselves.
- `BR-ADMIN-002` Admin cannot remove their own Admin role.
- `BR-ADMIN-003` Valid roles: `Admin` and `Customer` only.
- `BR-ADMIN-004` Deactivated user (`DeletedAt != null`) cannot log in.
- `BR-ADMIN-005` Unlock is only allowed on users with active lockout.

### Orders
- `BR-ADMIN-006` Status transitions allowed only per the lifecycle defined in `OrderStatus`.
- `BR-ADMIN-007` Orders with status `Delivered` or `Cancelled` cannot be changed.
- `BR-ADMIN-008` Event `OrderStatusUpdated` must be published after status change.

### Payments
- `BR-ADMIN-009` Only payments with status `Processed` can be refunded.
- `BR-ADMIN-010` Refund updates Payment to `Refunded` and Order to `Cancelled`.
- `BR-ADMIN-011` Event `PaymentRefunded` must be published after refund.

### Categories
- `BR-ADMIN-012` Slug must be unique in the system.
- `BR-ADMIN-013` Slug must be auto-generated from `name` if not provided.
- `BR-ADMIN-014` Category with active products cannot be deleted.
- `BR-ADMIN-015` Category deletion is soft delete (`DeletedAt`).

---

## Domain Events

| Event | Published when | Data |
|-------|---------------|------|
| `UserRoleAssigned` | Role assigned to user | `UserId`, `Role`, `OccurredAt` |
| `OrderStatusUpdated` | Order status changed by Admin | `OrderId`, `OldStatus`, `NewStatus`, `OccurredAt` |
| `PaymentRefunded` | Payment refunded | `PaymentId`, `OrderId`, `Amount`, `OccurredAt` |

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-ADMIN-U01 | Deactivate valid user | Existing UserId | `DeletedAt` set |
| AC-ADMIN-U02 | Admin deactivates themselves | UserId = AdminId | Business error |
| AC-ADMIN-U03 | Unlock user with lockout | UserId with active lockout | Lockout removed |
| AC-ADMIN-U04 | Unlock user without lockout | UserId without lockout | Business error |
| AC-ADMIN-U05 | Assign valid role | UserId + role `Admin` | Role assigned, event published |
| AC-ADMIN-U06 | Assign invalid role | UserId + role `SuperUser` | Validation error |
| AC-ADMIN-U07 | Update valid order status | OrderId + status `Shipped` | Status updated, event published |
| AC-ADMIN-U08 | Update delivered order status | OrderId with `Delivered` status | Business error |
| AC-ADMIN-U09 | Refund approved payment | PaymentId with `Processed` status | Status `Refunded`, Order `Cancelled` |
| AC-ADMIN-U10 | Refund pending payment | PaymentId with `Pending` status | Business error |
| AC-ADMIN-U11 | Create category with unique slug | Unique name + slug | Category created |
| AC-ADMIN-U12 | Create category with duplicate slug | Existing slug | Conflict error |
| AC-ADMIN-U13 | Delete category without products | CategoryId without products | Soft delete |
| AC-ADMIN-U14 | Delete category with active products | CategoryId with products | Business error |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-ADMIN-I01 | GET /admin/users as Admin | JWT Admin | `200 OK` + paginated list |
| AC-ADMIN-I02 | GET /admin/users as Customer | JWT Customer | `403 Forbidden` |
| AC-ADMIN-I03 | GET /admin/users without JWT | No token | `401 Unauthorized` |
| AC-ADMIN-I04 | GET /admin/users/{id} existing | Valid UserId | `200 OK` + details |
| AC-ADMIN-I05 | GET /admin/users/{id} non-existing | Invalid UserId | `404 Not Found` |
| AC-ADMIN-I06 | DELETE /admin/users/{id} | Valid UserId | `204 No Content` |
| AC-ADMIN-I07 | DELETE /admin/users self | Own AdminId | `400 Bad Request` |
| AC-ADMIN-I08 | POST /admin/users/{id}/unlock | UserId with lockout | `200 OK` |
| AC-ADMIN-I09 | POST /admin/users/{id}/roles | UserId + valid role | `200 OK` |
| AC-ADMIN-I10 | GET /admin/orders as Admin | JWT Admin | `200 OK` + paginated list |
| AC-ADMIN-I11 | GET /admin/orders/{id} existing | Valid OrderId | `200 OK` + details |
| AC-ADMIN-I12 | POST /admin/orders/{id}/status valid | Allowed status | `200 OK` |
| AC-ADMIN-I13 | POST /admin/orders/{id}/status invalid | Forbidden transition | `422 Unprocessable Entity` |
| AC-ADMIN-I14 | GET /admin/payments as Admin | JWT Admin | `200 OK` + paginated list |
| AC-ADMIN-I15 | POST /admin/payments/{id}/refund approved | PaymentId `Processed` | `200 OK` + status `Refunded` |
| AC-ADMIN-I16 | POST /admin/payments/{id}/refund pending | PaymentId `Pending` | `422 Unprocessable Entity` |
| AC-ADMIN-I17 | POST /admin/categories valid | Unique name + slug | `201 Created` |
| AC-ADMIN-I18 | POST /admin/categories duplicate slug | Existing slug | `409 Conflict` |
| AC-ADMIN-I19 | PUT /admin/categories/{id} | Valid data | `200 OK` |
| AC-ADMIN-I20 | DELETE /admin/categories/{id} no products | CategoryId without products | `204 No Content` |
| AC-ADMIN-I21 | DELETE /admin/categories/{id} with products | CategoryId with products | `422 Unprocessable Entity` |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| Auth (Phase 1) | Feature | Roles and JWT required |
| PostgreSQL | Infrastructure | Persistence |
| Dapper | Infrastructure | Listing queries |

## This feature is a dependency of
- Phase 2 (Catalog) — categories created here
- Phase 4 (Orders) — status management
- Phase 5 (Payments) — refund