# TECH-STACK.md
> Global context document. Lists all technologies, libraries, and versions used in the project.
> Skills and SPECs must reference this document when generating code.

---

## Runtime & Framework

| Technology | Version | Usage |
|------------|---------|-------|
| .NET | 10 | Main runtime |
| C# | 13 | Language |
| ASP.NET Core | 10 | Web framework |
| Minimal API | 10 | Endpoint definition |

---

## Authentication & Authorization

| Technology | Version | Usage |
|------------|---------|-------|
| ASP.NET Core Identity | 10 | User management, roles, lockout |
| JWT Bearer | 10 | Stateless authentication via token |
| BCrypt (via Identity) | — | Password hashing (automatic) |

### JWT Configuration
```csharp
TimeSpan.FromHours(1)   // Access Token duration
TimeSpan.FromDays(7)    // Refresh Token duration
SecurityAlgorithms.HmacSha256  // Algorithm

// Required claims
- sub   (UserId)
- email
- role
- jti   (JWT ID — for revocation)
```

---

## Database

| Technology | Version | Usage |
|------------|---------|-------|
| PostgreSQL | 16 | Main database |
| EF Core | 10 | ORM for Commands (write) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.2 | EF Core provider for PostgreSQL |
| EFCore.NamingConventions | 10.0.1 | snake_case naming convention |
| Dapper | 2.1.79 | Micro-ORM for Queries (read) |
| Npgsql | 10.0.3 | ADO.NET driver for Dapper |

### EF Core Conventions
```csharp
modelBuilder.UseSnakeCaseNamingConvention();

// Soft delete global filter
modelBuilder.Entity<Product>()
    .HasQueryFilter(p => p.DeletedAt == null);

// UUID primary key
property.HasDefaultValueSql("gen_random_uuid()");
```

---

## Cache

| Technology | Version | Usage |
|------------|---------|-------|
| Redis | 7 | Catalog cache, distributed Rate Limiting |
| StackExchange.Redis | 2.8.58 | Redis client for .NET — pinned to the 2.x line; `OpenTelemetry.Instrumentation.StackExchangeRedis` does not support 3.x |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.9 | IDistributedCache integration |

### Configured TTLs
```csharp
CacheKeys.Products      → TTL: 5 minutes
CacheKeys.ProductDetail → TTL: 10 minutes
CacheKeys.Categories    → TTL: 30 minutes
```

---

## Object Storage

| Technology | Version | Usage |
|------------|---------|-------|
| MinIO | latest | S3-compatible object storage, product images |
| AWSSDK.S3 | 4.0.25.1 | S3 client used against MinIO (`ServiceURL` + `ForcePathStyle`, swappable for real AWS S3/Cloudflare R2) |

Implemented via `IImageStorageService` (Domain) / `S3ImageStorageService` (Infrastructure). `BucketInitializer` ensures the target bucket exists on startup; `StorageHealthCheck` reports it on `GET /health`. See `docs/Tutorial.md` for the full worked example.

---

## Mediator & Validation

| Technology | Version | Usage |
|------------|---------|-------|
| MediatR | 14.1.0 | Mediator pattern for Commands and Queries |
| FluentValidation | 12.1.1 | Command and Query validation |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | DI registration for validators |
| FluentValidation.AspNetCore | 11.3.1 | ASP.NET Core pipeline integration |

### MediatR Pipeline
```
Request
  → TracingBehavior (ActivitySource span per request type)
  → LoggingBehavior
  → ValidationBehavior (FluentValidation)
  → Handler
  → Response
```

---

## Rate Limiting

| Technology | Version | Usage |
|------------|---------|-------|
| Microsoft.AspNetCore.RateLimiting | 10 (native) | Per-route rate limiting |

### Policies
```csharp
"auth-strict"   → FixedWindow:   5 req / 1 min  (login)
"auth-register" → FixedWindow:   3 req / 1 min  (register)
"public"        → SlidingWindow: 200 req / 1 min (catalog)
"user"          → SlidingWindow: 60 req / 1 min  (cart)
"orders"        → SlidingWindow: 20 req / 1 min  (orders)
"payment"       → SlidingWindow: 10 req / 1 min  (payments)
"upload"        → SlidingWindow: 5 req / 1 min   (product image upload)
```

---

## API Documentation

| Technology | Version | Usage |
|------------|---------|-------|
| Scalar | 2.x | Documentation UI (replaces Swagger UI) |
| Microsoft.AspNetCore.OpenApi | 10 (native) | OpenAPI 3.1 contract generation |

### Endpoints
```
GET /openapi/v1  → JSON OpenAPI 3.1 contract
GET /scalar      → Interactive UI (dev only)
```

---

## Observability

| Technology | Version | Usage |
|------------|---------|-------|
| Serilog | 4.3.1 | Structured logging |
| Serilog.Sinks.Seq | 9.1.0 | Log shipping to Seq |
| Serilog.Sinks.Grafana.Loki | 8.3.0 | Log shipping to Loki (also wired as a Grafana datasource alongside Jaeger) |
| Serilog.AspNetCore | 10.0.0 | ASP.NET Core / Serilog integration |
| Serilog.Sinks.Console | 6.1.1 | Console logs (dev) |
| OpenTelemetry.Extensions.Hosting | 1.16.0 | .NET host integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.15.2 | Automatic HTTP traces |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.15.1-beta.1 | EF Core query traces |
| OpenTelemetry.Instrumentation.StackExchangeRedis | 1.15.1-beta.2 | Redis cache call traces |
| OpenTelemetry.Instrumentation.AWS | 1.16.0 | AWS SDK (S3/MinIO) call traces |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.16.0 | Exports traces to Jaeger via OTLP/gRPC (no dedicated Jaeger exporter package — Jaeger receives OTLP directly) |
| OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.16.0-beta.1 | Exports metrics to Prometheus |

Custom spans (no extra package, plain `ActivitySource`):
- `Ecommerce.Application` source (`ApplicationActivitySource`) — one span per MediatR command/query (`TracingBehavior`), one span per domain event publish (`InMemoryEventBus`), and one span per Dapper query (`"Dapper {Class}.{Method}"`, tagged `db.system=postgresql`, `app.query.type=dapper`, and `db.statement=<the SQL text>`) in each `*QueryService`.

The raw `Npgsql` `ActivitySource` is **not** registered — it fires for every `NpgsqlCommand` regardless of caller, which duplicated EF Core spans and made Dapper/EF Core traces indistinguishable in Jaeger. EF Core writes are traced solely via `AddEntityFrameworkCoreInstrumentation()`; Dapper reads are traced solely via the manual spans above.

### Observability Endpoints
```
GET /health   → Health Check (postgres, redis, eventbus, storage/minio)
GET /metrics  → Prometheus scraping
```

---

## Tests

| Technology | Version | Usage |
|------------|---------|-------|
| xUnit | 2.9.3 | Test framework |
| xunit.runner.visualstudio | 3.1.4 | xUnit VS/CLI test runner |
| Moq | 4.20.72 | Dependency mocking |
| FluentAssertions | 8.10.0 | Readable assertions (v8+ — note the Xceed licensing change vs v6/v7) |
| Testcontainers.PostgreSql / .Redis / .Minio | 4.12.0 | Real PostgreSQL, Redis, and MinIO in integration tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | WebApplicationFactory for integration tests |
| Bogus | 35.6.5 | Fake data generation for tests |
| coverlet.collector | 6.0.4 | Code coverage collection |

`Ecommerce.SmokeTests` reuses the same xUnit/FluentAssertions/Bogus stack but has **no** Testcontainers dependency — it runs against the live `docker-compose` stack instead (`SMOKE_API_BASE_URL`, defaults to `http://localhost:8080`).

### Test naming pattern
```csharp
Should_Return_401_When_Password_Is_Wrong()
Should_Publish_PaymentRequested_When_Order_Is_Confirmed()
Should_Return_200_And_JWT_When_Login_Is_Valid()
Should_Return_429_When_Rate_Limit_Is_Exceeded()
```

---

## Docker

| Service | Image | Port |
|---------|-------|------|
| ecommerce-api | local build | 8080 |
| postgres | postgres:16-alpine | 5432 |
| redis | redis:7-alpine | 6379 |
| minio | minio/minio:latest | 9000 (S3 API) / 9001 (console) |
| seq | datalust/seq | 5341 / 80 |
| loki | grafana/loki:latest | 3100 |
| prometheus | prom/prometheus | 9090 |
| grafana | grafana/grafana | 3000 |
| jaeger | jaegertracing/all-in-one | 16686 / 4317 |

---

## NuGet Packages — Summary by Project

> The tables below mirror the actual `<PackageReference>` entries in each `.csproj` — keep them in sync when dependencies change instead of using floating `X.*` placeholders.

### Ecommerce.Domain
```xml
<PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="10.0.9" />
```

### Ecommerce.Application
```xml
<PackageReference Include="FluentValidation" Version="12.1.1" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
<PackageReference Include="MediatR" Version="14.1.0" />
```

### Ecommerce.Infrastructure
```xml
<PackageReference Include="AWSSDK.S3" Version="4.0.25.1" />
<PackageReference Include="Dapper" Version="2.1.79" />
<PackageReference Include="EFCore.NamingConventions" Version="10.0.1" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.9" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.0.9" />
<PackageReference Include="Npgsql" Version="10.0.3" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.2" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.16.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AWS" Version="1.16.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.15.1-beta.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.StackExchangeRedis" Version="1.15.1-beta.2" />
<PackageReference Include="Serilog" Version="4.3.1" />
<PackageReference Include="Serilog.Sinks.Seq" Version="9.1.0" />
<PackageReference Include="StackExchange.Redis" Version="2.8.58" />
```

### Ecommerce.API
```xml
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.1" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.9" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.9" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.9" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.16.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.16.0-beta.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.2" />
<PackageReference Include="Scalar.AspNetCore" Version="2.16.3" />
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.3.0" />
```

### Ecommerce.UnitTests / Ecommerce.SmokeTests
```xml
<PackageReference Include="Bogus" Version="35.6.5" />
<PackageReference Include="coverlet.collector" Version="6.0.4" />
<PackageReference Include="FluentAssertions" Version="8.10.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
<PackageReference Include="Moq" Version="4.20.72" />            <!-- not used by SmokeTests -->
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
```

### Ecommerce.IntegrationTests
```xml
<!-- same baseline as UnitTests, plus: -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9" />
<PackageReference Include="Testcontainers.Minio" Version="4.12.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.12.0" />
<PackageReference Include="Testcontainers.Redis" Version="4.12.0" />
```

---

## Environment Variables

```env
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8080
DB_CONNECTION_STRING=Host=postgres;Port=5432;Database=ecommerce;Username=postgres;Password=${DB_PASSWORD}
REDIS_CONNECTION_STRING=redis:6379
JWT_SECRET=<min 32 chars>
JWT_ISSUER=ecommerce-api
JWT_AUDIENCE=ecommerce-client
SEQ_URL=http://seq:5341
LOKI_URL=http://loki:3100
JAEGER_ENDPOINT=http://jaeger:4317
GRAFANA_PASSWORD=<grafana admin password>
ADMIN_EMAIL=admin@ecommerce.com
ADMIN_PASSWORD=<strong password>
CORS_ALLOWED_ORIGINS=<comma-separated browser origins, optional>
MINIO_ROOT_USER=<minio admin user>
MINIO_ROOT_PASSWORD=<minio admin password>
MINIO_ENDPOINT=http://minio:9000
MINIO_PUBLIC_URL=http://localhost:9000
MINIO_BUCKET_NAME=product-images
```

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [CONVENTIONS.md](./CONVENTIONS.md)