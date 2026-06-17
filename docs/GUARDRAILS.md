# GUARDRAILS.md
> Global project rules. No code, SPEC, or skill may violate these guidelines.

---

## 1. Architecture

### ✅ Required
- Strictly follow **Clean Architecture**: dependencies always point inward (API → Application → Domain).
- `Domain` must not reference any external layer (no EF Core, no HTTP, no third-party libs).
- All business logic lives in `Domain` or `Application`. Never in `API` or `Infrastructure`.
- Use **MediatR** for all use cases (Commands and Queries).
- Use **FluentValidation** for input validation on Commands/Queries.
- Repositories defined as interfaces in `Application`, implemented in `Infrastructure`.
- Follow **CQRS** pattern:
  - **Commands** (write) → EF Core
  - **Queries** (read) → Dapper

### ❌ Forbidden
- Accessing `DbContext` directly in the `API` layer.
- Placing business logic in Controllers or Minimal API handlers.
- Creating circular dependencies between layers.
- Referencing `Infrastructure` from `Application` or `Domain`.
- Using EF Core for read queries (use Dapper).
- Using Dapper for write operations (use EF Core).

---

## 2. Database (PostgreSQL + EF Core + Dapper)

### ✅ Required
- Use EF Core **Migrations** for every schema change.
- Name tables in **plural snake_case** (e.g. `users`, `order_items`).
- Every entity must have: `Id (UUID)`, `CreatedAt`, `UpdatedAt`.
- Use **soft delete** (`DeletedAt` field) on critical entities (Users, Orders, Products).
- Mandatory indexes on frequently searched columns (email, slug, etc.).
- **Dapper** exclusively for Queries (read) via `IDbConnection`.
- **EF Core** exclusively for Commands (write) via repositories.
- Every Dapper listing query must have pagination (`LIMIT` + `OFFSET`).

### ❌ Forbidden
- Using `.ToList()` without pagination on listing queries.
- Using raw SQL without documented justification.
- Altering the schema directly on the database (without migration).
- Leaving N+1 queries unresolved.
- Mixing Dapper and EF Core in the same operation.

---

## 3. Cache (Redis)

### ✅ Required
- Use **Redis** as cache layer via `ICacheService` (own abstraction).
- Implement **Cache-Aside** pattern for product catalog.
- Every cache key must be defined in `CacheKeys.cs` as a constant.
- Invalidate cache via **Domain Events** whenever a product is updated.
- Mandatory TTLs by data type:
  - Product list: **5 minutes**
  - Product detail: **10 minutes**
  - Categories: **30 minutes**
- Redis also used for **distributed Rate Limiting**.

### ❌ Forbidden
- Caching cart or order data.
- Creating cache keys without going through `CacheKeys.cs`.
- Cache without a defined TTL.
- Accessing Redis directly outside `ICacheService`.

---

## 4. Authentication (ASP.NET Core Identity + JWT)

### ✅ Required
- Use **ASP.NET Core Identity** for user management.
- Extend `IdentityUser<Guid>` with domain fields (`FirstName`, `LastName`, `CreatedAt`, `DeletedAt`).
- Use **JWT Bearer** for authenticating protected routes.
- JWT tokens with a maximum expiration of **1 hour**.
- **Refresh Token** generated and stored hashed in the database via `AspNetUserTokens`.
- Rotate Refresh Token on every use (invalidate the previous one).
- Configure Identity **Lockout**:
  - Maximum of **5 login attempts**
  - Lockout for **15 minutes**
- Password hashing managed automatically by Identity (bcrypt).
- Mandatory endpoint flow:
  - `POST /api/v1/auth/register` → creates user + publishes `UserRegistered`
  - `POST /api/v1/auth/login` → validates + returns JWT + Refresh Token
  - `POST /api/v1/auth/refresh` → renews JWT + rotates Refresh Token
  - `POST /api/v1/auth/logout` → revokes Refresh Token
  - `POST /api/v1/auth/forgot-password` → generates reset token (mocked email)
  - `POST /api/v1/auth/reset-password` → resets password + invalidates all Refresh Tokens
- **JWT required** on all routes, except:
  - `POST /auth/login`
  - `POST /auth/register`
  - `POST /auth/forgot-password`
  - `POST /auth/reset-password`
  - `GET /catalog/**` (public catalog)
- Validate and sanitize **all** inputs before processing.
- Use **HTTPS** in all environments (except local tests).
- **Scalar disabled in production**.

### ❌ Forbidden
- Manually implementing password hashing (Identity manages it).
- Storing Refresh Token in plain text.
- Returning different messages for "user does not exist" vs "wrong password" (avoid enumeration).
- Exposing stack traces in production responses.
- Returning internal error information to the client.
- Hardcoding secrets, connection strings, or API keys in code.
- Using `[AllowAnonymous]` without documented justification in the SPEC.
- Exposing Scalar outside the development environment.
- Disabling Identity Lockout.

---

## 5. Rate Limiting

### ✅ Required
- Use **.NET 10 native Rate Limiting** (`Microsoft.AspNetCore.RateLimiting`).
- Use **Sliding Window** for general API routes.
- Use **Fixed Window** for authentication routes.
- Limit by **IP** on public routes and by **UserId** on authenticated routes.
- Rate limit response must always return `429 Too Many Requests` with `Retry-After` header.
- Mandatory policies by route:

  | Route | Limit | Window |
  |-------|-------|--------|
  | `POST /auth/login` | 5 req | 1 min |
  | `POST /auth/register` | 3 req | 1 min |
  | `POST /auth/forgot-password` | 5 req | 1 min |
  | `POST /auth/reset-password` | 5 req | 1 min |
  | `GET /catalog/**` | 200 req | 1 min |
  | `POST /cart/**` | 60 req | 1 min |
  | `POST /orders/**` | 20 req | 1 min |
  | `POST /payments/**` | 10 req | 1 min |

### ❌ Forbidden
- Routes without a defined rate limiting policy.
- Returning an error other than `429` for exceeded rate limits.
- Disabling rate limiting in production.

---

## 6. API & Contracts

### ✅ Required
- Follow **RESTful** standard: correct HTTP verbs, semantic status codes.
- All error responses must follow the standard format:
  ```json
  {
    "type": "string",
    "title": "string",
    "status": 400,
    "errors": {
      "field": ["message"]
    },
    "traceId": "string"
  }
  ```
- Version the API via route prefix: `/api/v1/`.
- All listings must have **mandatory pagination** (pageNumber, pageSize, max 100 items).
- Use **snake_case** for JSON request/response fields.
- Document all endpoints in **Scalar** with summary, description, produces, and expected errors.

### ❌ Forbidden
- Returning lists without pagination.
- Using incorrect HTTP verbs.
- Exposing internal database IDs (use UUIDs).
- Returning `200 OK` for failed operations.
- Endpoints without Scalar documentation.

---

## 7. Documentation (Scalar)

### ✅ Required
- Use **Scalar** with **OpenAPI 3.1** for API documentation.
- Every endpoint must have: `WithName`, `WithSummary`, `WithDescription`, `Produces`.
- Scalar enabled **only in development**.
- OpenAPI contract available at `GET /openapi/v1`.
- Scalar UI available at `GET /scalar`.
- JWT authentication configured in Scalar for manual testing.

### ❌ Forbidden
- Enabling Scalar in production.
- Endpoints without OpenAPI documentation.
- Using Swashbuckle (replaced by Scalar in .NET 10).

---

## 8. Domain Events (Event-Driven)

### ✅ Required
- All events must implement the `IDomainEvent` interface.
- Events must be **immutable** (read-only properties).
- Event handlers must be **idempotent**.
- Name events in the past tense: `PaymentProcessed`, `OrderCreated`, `UserRegistered`.
- Every event must have: `EventId (UUID)`, `OccurredAt (DateTime)`.
- Payments mandatorily event-driven:
  - `PaymentRequested` → `PaymentProcessed` or `PaymentFailed`

### ❌ Forbidden
- Publishing events from the `API` layer.
- Events with embedded business logic.
- Handlers that depend on other handlers directly.
- Processing payments synchronously.

---

## 9. Observability

### ✅ Required
- Use **OpenTelemetry** as the standard for logs, metrics, and traces.
- **Serilog** for structured logging.
- All three pillars are mandatory:
  - **Logs** → Seq (local) / Azure Monitor (production)
  - **Metrics** → Prometheus + Grafana
  - **Traces** → Jaeger (local) / Azure Monitor (production)
- Expose **Health Check** endpoint at `GET /health` with status of:
  - PostgreSQL
  - Redis
  - Event Bus
- Mandatory logging: authentication errors, payment failures, 5xx errors.
- Mandatory metrics: req/sec, p95/p99 latency, error rate, cache hit ratio.

### ❌ Forbidden
- Using `Console.WriteLine` for logging (use Serilog).
- Exposing sensitive data (passwords, tokens) in logs.
- Deploying without health check configured.
- Disabling traces in production.

---

## 10. Docker

### ✅ Required
- **The entire project must run via Docker**.
- Use `docker-compose.yml` as the local orchestrator.
- Mandatory services in docker-compose:
  - `ecommerce-api` (.NET 10)
  - `postgres`
  - `redis`
  - `seq`
  - `prometheus`
  - `grafana`
  - `jaeger`
- Use **multi-stage build** in the API Dockerfile.
- Environment variables via `.env` (never hardcoded in compose).
- `docker-compose.override.yml` for development settings.
- `docker-compose.prod.yml` for production settings.

### ❌ Forbidden
- Running any service outside Docker in production.
- Hardcoding passwords or secrets in `docker-compose.yml`.
- Using `latest` as image tag in production.
- Committing the `.env` file to the repository.

---

## 11. Tests

### ✅ Required
- Every validation criterion defined in a `SPEC-*.md` **must** have a corresponding test.
- Use **xUnit** as the test framework.
- Use **Moq** for mocks.
- Use **FluentAssertions** for assertions.
- Unit tests go in `Ecommerce.UnitTests`.
- Integration tests go in `Ecommerce.IntegrationTests`.
- Name tests following the pattern: `Should_[Result]_When_[Condition]`.
- Tests generated by the `spec-to-tests` skill from the SPEC `Validation Criteria`.

### ❌ Forbidden
- Tests that depend on execution order.
- Tests that access a real database (use TestContainers).
- Creating production code without a corresponding validation criterion in the SPEC.
- Submitting a PR with failing tests.

---

## 12. C# Code

### ✅ Required
- Use **C# 13** with nullable reference types enabled.
- Use `record` for DTOs and Value Objects.
- Use `sealed` on classes that should not be inherited.
- Document public methods with XML comments.
- Follow Microsoft naming conventions.

### ❌ Forbidden
- Using `var` where the type is not obvious.
- Suppressing compiler warnings without justification.
- Using `dynamic` or `object` without strong justification.
- Methods longer than 30 lines.

---

## 13. Skills

### ✅ Required
- Every skill must explicitly declare which context documents it reads.
- Every skill must have an `## Output` section describing exactly what it generates.
- The `spec-to-tests` skill may only generate tests based on the SPEC `Validation Criteria`.

### ❌ Forbidden
- Skills that assume undocumented context.
- Skills that generate code outside the standards defined in this GUARDRAILS.

---

## References

- [ARCHITECTURE.md](./context/ARCHITECTURE.md)
- [CONVENTIONS.md](./context/CONVENTIONS.md)
- [TECH-STACK.md](./context/TECH-STACK.md)
- [DOMAIN-GLOSSARY.md](./context/DOMAIN-GLOSSARY.md)
- [EVENT-PATTERNS.md](./context/EVENT-PATTERNS.md)