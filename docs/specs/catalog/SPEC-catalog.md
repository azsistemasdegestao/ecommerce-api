# SPEC-catalog.md
> Feature: Product Catalog
> Phase: 2
> Status: Draft

## Context
- [CONTEXT-catalog.md](./CONTEXT-catalog.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Manages the catalog of products available for sale.
Read routes are public (no JWT). Write routes require `Admin` role.
Redis cache applied to listings and product details.

---

## Endpoints

### GET /api/v1/catalog/products
Lists available products with pagination and filters.

- **Auth:** Public
- **Rate Limit:** `public` — 200 req/min per IP
- **Cache:** Redis, TTL 5 minutes

**Query Params:**
```
page_number   int     default: 1
page_size     int     default: 20, max: 100
category_slug string  optional
search        string  optional
min_price     decimal optional
max_price     decimal optional
in_stock      bool    optional
```

**Response 200 OK:**
```json
{
  "items": [
    {
      "id": "uuid",
      "name": "Blue T-Shirt M",
      "slug": "blue-t-shirt-m",
      "price": 29.90,
      "image_url": "https://...",
      "category": { "id": "uuid", "name": "T-Shirts", "slug": "t-shirts" },
      "in_stock": true
    }
  ],
  "total_count": 200,
  "page_number": 1,
  "page_size": 20,
  "total_pages": 10
}
```

---

### GET /api/v1/catalog/products/{slug}
Returns full details of a product by slug.

- **Auth:** Public
- **Cache:** Redis, TTL 10 minutes

**Response 200 OK:**
```json
{
  "id": "uuid",
  "name": "Blue T-Shirt M",
  "slug": "blue-t-shirt-m",
  "description": "Cotton blue t-shirt, size M.",
  "price": 29.90,
  "stock": 50,
  "image_url": "https://...",
  "in_stock": true,
  "category": { "id": "uuid", "name": "T-Shirts", "slug": "t-shirts" },
  "created_at": "2024-01-01T00:00:00Z",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 404 | Product not found or deleted |

---

### GET /api/v1/catalog/categories
Lists all available categories.

- **Auth:** Public
- **Cache:** Redis, TTL 30 minutes

---

### POST /api/v1/catalog/products
Creates a new product.

- **Auth:** JWT + Role `Admin`

**Request:**
```json
{
  "name": "Blue T-Shirt M",
  "description": "Cotton blue t-shirt, size M.",
  "slug": "blue-t-shirt-m",
  "price": 29.90,
  "stock": 50,
  "image_url": "https://...",
  "category_id": "uuid"
}
```

**Response 201 Created**

**Errors:**
| Status | Reason |
|--------|--------|
| 409 | Slug already exists |
| 422 | Price or stock invalid |

---

### POST /api/v1/catalog/products/{id}/image
Uploads an image for an existing product.

- **Auth:** JWT + Role `Admin`
- **Rate Limit:** `upload` — 5 req/min

**Request:** `multipart/form-data`, field `file` (`image/jpeg`, `image/png` or `image/webp`, max 5MB)

**Response 200 OK:**
```json
{
  "id": "uuid",
  "image_url": "https://...",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | Invalid content type or file size exceeded |
| 404 | Product not found |

---

### PUT /api/v1/catalog/products/{id}
Updates an existing product.

- **Auth:** JWT + Role `Admin`

### DELETE /api/v1/catalog/products/{id}
Removes a product (soft delete).

- **Auth:** JWT + Role `Admin`

---

## Business Rules

- `BR-CAT-001` Slug must be unique in the system.
- `BR-CAT-002` Slug auto-generated from `name` if not provided.
- `BR-CAT-003` Price must be greater than zero.
- `BR-CAT-004` Stock cannot be negative.
- `BR-CAT-005` Deleted product (`DeletedAt != null`) does not appear in the public catalog.
- `BR-CAT-006` `CategoryId` must reference an existing, non-deleted category.
- `BR-CAT-007` Event `ProductCreated` published after successful creation — invalidates Redis listing cache.
- `BR-CAT-008` Event `ProductUpdated` published after update — invalidates Redis cache.
- `BR-CAT-009` Event `ProductDeleted` published after deletion — invalidates Redis cache.
- `BR-CAT-010` Listings always paginated (maximum 100 items per page).
- `BR-CAT-011` Cache applied on public queries (Cache-Aside Pattern).
- `BR-CAT-012` Image upload accepts only `image/jpeg`, `image/png`, `image/webp`, max 5MB.
- `BR-CAT-013` `ProductUpdated` event reused after image upload, invalidates cache.

---

## Domain Events

| Event | Published when | Effect |
|-------|---------------|--------|
| `ProductCreated` | Product created | Invalidates Redis cache (listing) |
| `ProductUpdated` | Product updated | Invalidates Redis cache |
| `ProductDeleted` | Product deleted | Invalidates Redis cache |

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-CAT-U01 | Create product with valid data | All valid fields | Product created, `ProductCreated` published |
| AC-CAT-U02 | Create product with duplicate slug | Existing slug | Conflict error |
| AC-CAT-U03 | Create product with zero price | `price: 0` | Validation error |
| AC-CAT-U04 | Create product with negative stock | `stock: -1` | Validation error |
| AC-CAT-U05 | Create product with non-existing category | Invalid `category_id` | Business error |
| AC-CAT-U06 | Update existing product | Valid data | Product updated, `ProductUpdated` published |
| AC-CAT-U07 | Delete existing product | Valid ProductId | Soft delete, `ProductDeleted` published |
| AC-CAT-U08 | Cache invalidated after update | ProductUpdated handler | Redis keys removed |
| AC-CAT-U09 | Auto-generated slug when absent | Name without slug | Slug correctly generated |
| AC-CAT-U10 | Upload image for existing product | Valid file + ProductId | Image uploaded, product updated, `ProductUpdated` published |
| AC-CAT-U11 | Upload image for non-existing product | Invalid ProductId | `NotFoundException`, upload never attempted |
| AC-CAT-U12 | Cache invalidated after creation | ProductCreated handler | Redis listing cache key removed |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-CAT-I01 | GET /catalog/products no filters | — | `200 OK` + paginated list |
| AC-CAT-I02 | GET /catalog/products with category_slug filter | Valid slug | `200 OK` + filtered products |
| AC-CAT-I03 | GET /catalog/products with in_stock filter | `in_stock=true` | `200 OK` + in-stock products only |
| AC-CAT-I04 | GET /catalog/products with page_size > 100 | `page_size=200` | `400 Bad Request` |
| AC-CAT-I05 | GET /catalog/products/{slug} existing | Valid slug | `200 OK` + details |
| AC-CAT-I06 | GET /catalog/products/{slug} non-existing | Invalid slug | `404 Not Found` |
| AC-CAT-I07 | GET /catalog/products/{slug} deleted | Soft-deleted product | `404 Not Found` |
| AC-CAT-I08 | GET /catalog/categories | — | `200 OK` + categories list |
| AC-CAT-I09 | GET /catalog/products cache hit | Second identical request | Returned from Redis |
| AC-CAT-I10 | Cache invalidated after PUT | Update product + GET | Updated data returned |
| AC-CAT-I11 | POST /catalog/products as Admin | JWT Admin + valid data | `201 Created` |
| AC-CAT-I12 | POST /catalog/products as Customer | JWT Customer | `403 Forbidden` |
| AC-CAT-I13 | POST /catalog/products without JWT | No token | `401 Unauthorized` |
| AC-CAT-I14 | POST /catalog/products duplicate slug | Existing slug | `409 Conflict` |
| AC-CAT-I15 | PUT /catalog/products/{id} as Admin | JWT Admin + valid data | `200 OK` |
| AC-CAT-I16 | PUT /catalog/products/{id} non-existing | Invalid ProductId | `404 Not Found` |
| AC-CAT-I17 | DELETE /catalog/products/{id} as Admin | JWT Admin | `204 No Content` |
| AC-CAT-I18 | Rate limit public catalog | 201 req/min | `429 Too Many Requests` |
| AC-CAT-I19 | POST /catalog/products/{id}/image as Admin | Valid jpeg file | `200 OK` + `image_url` |
| AC-CAT-I20 | POST /catalog/products/{id}/image non-existing product | Invalid ProductId | `404 Not Found` |
| AC-CAT-I21 | POST /catalog/products/{id}/image invalid content type | `.txt` file | `400 Bad Request` |
| AC-CAT-I22 | POST /catalog/products/{id}/image oversized file | File > 5MB | `400 Bad Request` |
| AC-CAT-I23 | POST /catalog/products/{id}/image as Customer | JWT Customer | `403 Forbidden` |
| AC-CAT-I24 | POST /catalog/products/{id}/image without JWT | No token | `401 Unauthorized` |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| Admin (Phase 1.5) | Feature | Categories created by Admin |
| PostgreSQL | Infrastructure | Persistence |
| Redis | Infrastructure | Listing cache |
| Dapper | Infrastructure | Read queries |

## This feature is a dependency of
- Phase 3 (Cart) — looks up products to add
- Phase 4 (Orders) — product snapshot in OrderItem