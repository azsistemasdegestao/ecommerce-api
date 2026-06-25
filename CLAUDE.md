# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build Ecommerce.slnx

# Run API (http://localhost:5027 or https://localhost:7197)
dotnet run --project Ecommerce.API

# Run all tests
dotnet test Ecommerce.slnx

# Run a single test project
dotnet test Ecommerce.UnitTests/Ecommerce.UnitTests.csproj
dotnet test Ecommerce.IntegrationTests/Ecommerce.IntegrationTests.csproj
dotnet test Ecommerce.SmokeTests/Ecommerce.SmokeTests.csproj  # requires the live Docker stack — NEVER run automatically, only when the user explicitly asks

# Run via Docker (full stack: API, PostgreSQL, Redis, MinIO, Seq, Loki, Prometheus, Grafana, Jaeger)
docker-compose up
```

EF Core migrations go in `Ecommerce.Infrastructure`. Named `[timestamp]_[Description].cs`.

## Architecture

**Clean Architecture + CQRS + Event-Driven** (payments only).

```
API → Application → Domain
Infrastructure (injected via DI — never referenced by Application or Domain)
```

Layer responsibilities:
- **Domain** — entities, events, repository/service interfaces. Zero external dependencies.
- **Application** — MediatR commands/queries/handlers, FluentValidation validators, DTOs. Knows only Domain.
- **Infrastructure** — EF Core, Dapper, Redis, Identity, EventBus implementations. Injected at startup.
- **API** — Minimal API endpoints, middleware, DI wiring. Never contains business logic.

### CQRS Split (hard rule)

| Operation | ORM | Layer |
|-----------|-----|-------|
| Commands (writes) | EF Core via repository | Application → Infrastructure |
| Queries (reads) | Dapper via `IDbConnection` | Application → Infrastructure |

Queries return projected DTOs — never domain entities. Every listing query must paginate (`LIMIT` + `OFFSET`). Never use `.ToList()` without pagination.

### Event-Driven (Payments)

Payments are async. `POST /payments` dispatches `RequestPaymentCommand` → publishes `PaymentRequested` event → `PaymentRequestedHandler` calls `MockGatewayService` → publishes `PaymentProcessed` or `PaymentFailed`. Do not publish events from the API layer.

## File Structure by Feature

```
Application/[Feature]/
  Commands/[Action][Entity]/
    [Action][Entity]Command.cs
    [Action][Entity]Handler.cs
    [Action][Entity]Validator.cs
    [Action][Entity]Response.cs
  Queries/Get[Entity]/
    Get[Entity]Query.cs
    Get[Entity]Handler.cs
  EventHandlers/
    [Event]Handler.cs
```

## Naming Conventions

| Artifact | Pattern | Example |
|----------|---------|---------|
| Command | `[Action][Entity]Command` | `CreateOrderCommand` |
| Query | `Get[Entity/Entities]Query` | `GetOrdersQuery` |
| Handler | `[Command/Query without suffix]Handler` | `CreateOrderHandler` |
| Validator | `[Command/Query]Validator` | `CreateOrderValidator` |
| DTO | `[Entity][Context]Dto` (record) | `OrderSummaryDto` |
| Repository impl | `[Entity]Repository` | `OrderRepository` |
| Dapper service | `[Entity]QueryService` | `OrderQueryService` |
| Endpoint class | `[Feature]Endpoints` | `OrdersEndpoints` |
| Domain event | `[Entity][PastAction]` (sealed record) | `OrderCreated` |

Private fields: `_camelCase`. Constants: `PascalCase`. Enums: `PascalCase` for type and value.

## Key Guardrails

**Architecture:**
- `Domain` must have zero external NuGet dependencies.
- Never access `DbContext` in the API layer.
- Never reference `Infrastructure` from `Application` or `Domain`.

**Database:**
- Tables: plural snake_case (`order_items`, `cart_items`). Columns: snake_case.
- Every entity requires `Id (UUID)`, `CreatedAt`, `UpdatedAt`. Soft delete via `DeletedAt` on Users, Orders, Products.
- Indexes named `idx_[table]_[column]`.
- Every schema change requires an EF Core migration — never alter DB directly.

**Cache:**
- All cache keys must be constants in `CacheKeys.cs`. Cache without TTL is forbidden.
- Never cache cart or order data. Cache only via `ICacheService` abstraction (not Redis directly).
- TTLs: product list 5 min, product detail 10 min, categories 30 min.
- Invalidate cache via domain events when products are updated.

**Auth:**
- JWT max 1 hour. Refresh Token stored hashed in `AspNetUserTokens`, rotated on every use.
- Identity lockout: 5 attempts, 15-minute lock. Never disable lockout.
- Return identical messages for "user not found" vs "wrong password" (prevent enumeration).
- Scalar is development-only.

**C# style:**
- C# 13, nullable reference types enabled. Use `record` for DTOs and Value Objects. Use `sealed` on non-inheritable classes.
- Avoid `var` when the type is not obvious. Methods must not exceed 30 lines.
- Always pass `CancellationToken` in async methods. Never use `.Result` or `.Wait()`.
- Use structured logging: `_logger.LogInformation("Order {OrderId} created", order.Id)` — never string interpolation in log calls.
- Never use `Console.WriteLine` (use Serilog).

**API contracts:**
- All JSON fields in `snake_case` (configured globally via `JsonNamingPolicy.SnakeCaseLower`).
- POST (created) → 201 + Location header. Async POST (payment) → 202. DELETE → 204.
- Rate limiting: every route must have a policy; exceeded limit always returns 429 with `Retry-After`.
- Every endpoint needs `WithName`, `WithSummary`, `WithDescription`, `Produces`.

**Security headers & CORS:**
- `Ecommerce.API/Extensions/SecurityExtensions.cs` applies `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy` to every response — don't remove `app.UseSecurityHeaders()` from `Program.cs`.
- CORS is closed by default; only origins listed in `CORS_ALLOWED_ORIGINS` (comma-separated) are allowed. No origins configured = no cross-origin browser access (same-origin/non-browser calls are unaffected).

**Tests:**
- Naming: `Should_[Result]_When_[Condition]`.
- Unit tests in `Ecommerce.UnitTests`, integration tests in `Ecommerce.IntegrationTests`, smoke tests against the live Docker stack in `Ecommerce.SmokeTests` (auth, catalog cache, error scenarios, load/latency, full purchase flow).
- Use Moq for mocks, FluentAssertions for assertions, TestContainers for real PostgreSQL/Redis in integration tests.
- Tests must not depend on execution order. Integration tests use `CustomWebApplicationFactory` + `TestContainersFixture`.
- `Ecommerce.IntegrationTests/xunit.runner.json` sets `parallelizeTestCollections: false` — each test class spins up its own Postgres+Redis Testcontainers, and running them in parallel causes flaky connection-timeout failures under load. If integration tests fail in a full run but pass when filtered to one class, suspect this setting was removed before suspecting a real bug.

## Available Skills

Custom skills in `skills/` automate common tasks:

| Skill | Purpose |
|-------|---------|
| `scaffold-feature` | Scaffolds the full file structure for a new feature |
| `spec-to-tests` | Generates xUnit tests from a SPEC's Validation Criteria |
| `validate-spec` | Validates that a SPEC conforms to project standards |
| `event-handler` | Scaffolds a domain event + handler pair |

## Key Documentation

Full context lives in `docs/`:
- `docs/GUARDRAILS.md` — authoritative rules; no code may violate them
- `docs/PLAN.md` — phased implementation roadmap
- `docs/context/ARCHITECTURE.md` — detailed architecture and request flow
- `docs/context/TECH-STACK.md` — all libraries, versions, and NuGet packages by project
- `docs/context/CONVENTIONS.md` — naming and code structure with examples
- `docs/context/EVENT-PATTERNS.md` — event-driven patterns
- `docs/context/DOMAIN-GLOSSARY.md` — domain terminology
- `docs/specs/` — per-feature specs (auth, admin, catalog, cart, orders, payments)
