# SPEC-payments.md
> Feature: Payments (Event-Driven)
> Phase: 5
> Status: Draft

## Context
- [CONTEXT-payments.md](./CONTEXT-payments.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)

---

## Overview

Processes payments **asynchronously** via domain events.
The payment gateway is **mocked** (`MockGateway`) — 80% approval, 20% failure.

---

## Endpoints

### POST /api/v1/payments
Initiates the payment process for an Order.

- **Auth:** JWT required
- **Rate Limit:** `payment` — 10 req/min per UserId

**Request:**
```json
{ "order_id": "uuid", "payment_method": "CreditCard" }
```
`payment_method` is one of `CreditCard`, `Pix`, `Boleto` (case-insensitive).

**Response 202 Accepted:**
```json
{
  "payment_id": "uuid",
  "order_id": "uuid",
  "amount": 59.80,
  "status": "Pending",
  "payment_method": "CreditCard",
  "message": "Payment is being processed."
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 400 | `payment_method` is not one of `CreditCard`, `Pix`, `Boleto` |
| 404 | Order not found |
| 422 | Order is not in Pending status |
| 422 | Order does not belong to Customer |

---

### GET /api/v1/payments/{orderId}
Checks the payment status for an Order.

- **Auth:** JWT required

**Response 200 OK:**
```json
{
  "id": "uuid",
  "order_id": "uuid",
  "amount": 59.80,
  "status": "Processed",
  "provider": "MockGateway",
  "payment_method": "CreditCard",
  "created_at": "2024-01-01T00:00:00Z",
  "updated_at": "2024-01-01T00:00:00Z"
}
```

**Errors:**
| Status | Reason |
|--------|--------|
| 403 | Payment does not belong to Customer |
| 404 | Payment not found |

---

## Business Rules

- `BR-PAY-001` Only Orders with `Pending` status can be paid.
- `BR-PAY-002` Customer may only pay their own Orders.
- `BR-PAY-003` Payment created with `Pending` status — return `202 Accepted` immediately.
- `BR-PAY-004` Processing via `MockGateway`: approval rate and settlement delay vary by `payment_method` — `CreditCard` 80% approval/100–500ms, `Pix` 95% approval/100–300ms, `Boleto` 70% approval/500–1500ms.
- `BR-PAY-009` `payment_method` is required and must be one of `CreditCard`, `Pix`, `Boleto` — otherwise `400 Bad Request`.
- `BR-PAY-005` Event handlers must be **idempotent**.
- `BR-PAY-006` `PaymentProcessed` → Order status `Confirmed`.
- `BR-PAY-007` `PaymentFailed` → Order status `Cancelled`.
- `BR-PAY-008` Refund (`PaymentRefunded`) available only via Admin (Phase 1.5).

---

## Domain Events

| Event | Published by | Consumed by | Effect |
|-------|-------------|-------------|--------|
| `PaymentRequested` | `RequestPaymentHandler` | `PaymentRequestedHandler` | Starts gateway processing |
| `PaymentProcessed` | `PaymentRequestedHandler` | `PaymentProcessedHandler` | Order → `Confirmed` |
| `PaymentFailed` | `PaymentRequestedHandler` | `PaymentFailedHandler` | Order → `Cancelled` |
| `PaymentRefunded` | `RefundPaymentHandler` (Admin) | `PaymentRefundedHandler` | Order → `Cancelled` |

---

## Full Flow

```
POST /payments
      ↓
RequestPaymentCommand
      ↓
RequestPaymentHandler
  - Validates Order (Pending, belongs to Customer)
  - Creates Payment (Status: Pending)
  - Persists via EF Core
  - Publishes PaymentRequested
  - Returns 202
      ↓
PaymentRequestedHandler
  - Checks idempotency (EventId)
  - Updates Payment (Status: Processing)
  - Calls MockGatewayService (delay 100-500ms)
      ↓ 80%                    ↓ 20%
PaymentProcessed           PaymentFailed
      ↓                         ↓
PaymentProcessedHandler    PaymentFailedHandler
  - Payment → Processed      - Payment → Failed
  - Order → Confirmed        - Order → Cancelled
```

---

## Validation Criteria

### Unit Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-PAY-U01 | Request payment for valid Pending Order | Pending OrderId + correct UserId | Payment created, `PaymentRequested` published |
| AC-PAY-U02 | Request payment for Confirmed Order | Confirmed OrderId | Business error |
| AC-PAY-U03 | Request payment for another Customer's Order | Other's OrderId | Authorization error |
| AC-PAY-U04 | PaymentRequested handler approves payment | MockGateway returns success | `PaymentProcessed` published |
| AC-PAY-U05 | PaymentRequested handler rejects payment | MockGateway returns failure | `PaymentFailed` published |
| AC-PAY-U06 | PaymentProcessed handler updates Order | PaymentProcessed event | Order status = Confirmed |
| AC-PAY-U07 | PaymentFailed handler updates Order | PaymentFailed event | Order status = Cancelled |
| AC-PAY-U08 | Idempotency — same event twice | Duplicate EventId | Second processing ignored |

### Integration Tests

| ID | Scenario | Input | Expected |
|----|----------|-------|----------|
| AC-PAY-I01 | POST /payments valid Pending Order | JWT + Pending OrderId | `202 Accepted` + payment_id |
| AC-PAY-I02 | POST /payments Confirmed Order | JWT + Confirmed OrderId | `422 Unprocessable Entity` |
| AC-PAY-I03 | POST /payments without JWT | No token | `401 Unauthorized` |
| AC-PAY-I04 | POST /payments non-existing Order | Invalid OrderId | `404 Not Found` |
| AC-PAY-I05 | POST /payments another Customer's Order | Other's OrderId | `422 Unprocessable Entity` |
| AC-PAY-I06 | GET /payments/{orderId} after approval | OrderId with payment | `200 OK` + status Processed |
| AC-PAY-I07 | GET /payments/{orderId} after failure | OrderId with failure | `200 OK` + status Failed |
| AC-PAY-I08 | GET /payments/{orderId} without JWT | No token | `401 Unauthorized` |
| AC-PAY-I09 | GET /payments/{orderId} another Customer's | Other's OrderId | `403 Forbidden` |
| AC-PAY-I10 | Full flow approved | POST /payments → GET /orders/{id} | Order status = Confirmed |
| AC-PAY-I11 | Full flow rejected | POST /payments → GET /orders/{id} | Order status = Cancelled |
| AC-PAY-I12 | Rate limit payments | 11 req/min | `429 Too Many Requests` |
| AC-PAY-I13 | Idempotency end-to-end | POST /payments twice on same Order | Second returns `422` |

---

## Dependencies

| Dependency | Type | Reason |
|------------|------|--------|
| Auth (Phase 1) | Feature | JWT required |
| Orders (Phase 4) | Feature | Order is the Payment target |
| PostgreSQL | Infrastructure | Persistence |
| IEventBus | Infrastructure | Async flow |
| MockGatewayService | Infrastructure | Gateway simulation |