# Ecommerce API

B2C ecommerce REST API built with .NET 10, Clean Architecture, CQRS, and event-driven payments.

## Features

| Feature | Endpoints |
|---------|-----------|
| Auth | `register`, `login`, `refresh`, `logout`, `forgot-password`, `reset-password` |
| Admin | user management, category/order/payment management — all under `/api/v1/admin/**`, role `Admin` only |
| Catalog | public product/category browsing with Redis cache-aside; admin product/category CRUD; admin product image upload (MinIO/S3) |
| Cart | add/update/remove items, clear cart, get cart — `/api/v1/cart/**` |
| Orders | checkout from cart, list/view/cancel orders — `/api/v1/orders/**` |
| Payments | async payment processing via `MockGateway` (event-driven), check status, admin refund — `/api/v1/payments/**` |

See `docs/specs/` for the full per-feature SPEC (business rules, validation criteria) and `docs/PLAN.md` for implementation status.

## Quick Start

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local development)

### Run with Docker

```bash
cp .env.example .env
# Edit .env with your values
docker-compose up
```

| Service | URL |
|---------|-----|
| API | http://localhost:8080 |
| Scalar UI | http://localhost:8080/scalar/v1 |
| OpenAPI JSON | http://localhost:8080/openapi/v1 |
| Health Check | http://localhost:8080/health |
| Metrics | http://localhost:8080/metrics |
| Seq (logs) | http://localhost:8082 |
| Loki (logs, Grafana datasource) | http://localhost:3100 |
| MinIO Console (object storage) | http://localhost:9001 (S3 API on :9000) |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 (admin / value of `GRAFANA_PASSWORD`, provisioned dashboard: "Ecommerce API") |
| Jaeger | http://localhost:16686 |

### Run locally

```bash
dotnet restore
dotnet run --project Ecommerce.API
```

Requires PostgreSQL and Redis running locally, or override connection strings in `appsettings.Development.json`.

### Database migrations

```bash
dotnet ef migrations add <MigrationName> --project Ecommerce.Infrastructure --startup-project Ecommerce.API
dotnet ef database update --project Ecommerce.Infrastructure --startup-project Ecommerce.API
```

### Tests

```bash
dotnet test                                              # all tests
dotnet test Ecommerce.UnitTests                         # unit tests only
dotnet test Ecommerce.IntegrationTests                  # integration tests only (requires Docker)
dotnet test --filter "FullyQualifiedName~Auth"          # filter by feature
dotnet test Ecommerce.SmokeTests                        # smoke tests against the live Docker stack (requires `docker-compose up`)
```

Integration test collections run sequentially (`Ecommerce.IntegrationTests/xunit.runner.json`) — each test class spins up its own Postgres + Redis Testcontainers, and running them all in parallel can starve Docker/DB connections on resource-constrained machines.

## Security

- Every route has a rate limiting policy (`Ecommerce.API/Extensions/RateLimitingExtensions.cs`); exceeding it returns `429` with `Retry-After`.
- Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`) are applied to every response.
- CORS is closed by default. Set `CORS_ALLOWED_ORIGINS` (comma-separated) to allow specific browser origins.
- Scalar/OpenAPI are mounted only when `ASPNETCORE_ENVIRONMENT=Development`; both return `404` in Production (`docker-compose.prod.yml` sets `ASPNETCORE_ENVIRONMENT=Production`).
- `GET /health` reports PostgreSQL, Redis, the event bus, and object storage (MinIO).

## Architecture

Clean Architecture + CQRS. See [`docs/context/ARCHITECTURE.md`](docs/context/ARCHITECTURE.md).

```
API → Application → Domain
Infrastructure (injected via DI)
```

- **Commands** (writes) → EF Core
- **Queries** (reads) → Dapper + Redis cache-aside
- **Payments** → event-driven via `IEventBus`

## Project Structure

```
Ecommerce.Domain/          # Entities, events, interfaces — zero external deps
Ecommerce.Application/     # MediatR commands/queries, FluentValidation, DTOs
Ecommerce.Infrastructure/  # EF Core, Dapper, Redis, Identity, EventBus
Ecommerce.API/             # Minimal API endpoints, middleware, DI wiring
Ecommerce.UnitTests/       # xUnit + Moq + FluentAssertions
Ecommerce.IntegrationTests/# xUnit + TestContainers + WebApplicationFactory
Ecommerce.SmokeTests/      # xUnit checks against the live Docker stack (auth, cache, errors, load, purchase flow)
docs/                      # Architecture, specs, conventions, guardrails
skills/                    # Claude Code skills for spec-driven development
```
