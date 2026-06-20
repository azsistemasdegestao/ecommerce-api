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
| Npgsql.EntityFrameworkCore.PostgreSQL | 10 | EF Core provider for PostgreSQL |
| Dapper | 2.x | Micro-ORM for Queries (read) |
| Npgsql | 8.x | ADO.NET driver for Dapper |

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
| StackExchange.Redis | 2.x | Redis client for .NET |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10 | IDistributedCache integration |

### Configured TTLs
```csharp
CacheKeys.Products      → TTL: 5 minutes
CacheKeys.ProductDetail → TTL: 10 minutes
CacheKeys.Categories    → TTL: 30 minutes
```

---

## Mediator & Validation

| Technology | Version | Usage |
|------------|---------|-------|
| MediatR | 12.x | Mediator pattern for Commands and Queries |
| FluentValidation | 11.x | Command and Query validation |
| FluentValidation.AspNetCore | 11.x | ASP.NET Core pipeline integration |

### MediatR Pipeline
```
Request
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
| Serilog | 4.x | Structured logging |
| Serilog.Sinks.Seq | 8.x | Log shipping to Seq |
| Serilog.Sinks.Grafana.Loki | 8.x | Log shipping to Loki (Grafana datasource) |
| Serilog.Sinks.Console | 5.x | Console logs (dev) |
| OpenTelemetry | 1.x | Standard for metrics and traces |
| OpenTelemetry.Extensions.Hosting | 1.x | .NET host integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.x | Automatic HTTP traces |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.x | EF Core query traces |
| OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.x | Exports metrics to Prometheus |
| OpenTelemetry.Exporter.Jaeger | 1.x | Exports traces to Jaeger |

### Observability Endpoints
```
GET /health   → Health Check (postgres, redis, eventbus)
GET /metrics  → Prometheus scraping
```

---

## Tests

| Technology | Version | Usage |
|------------|---------|-------|
| xUnit | 2.x | Test framework |
| Moq | 4.x | Dependency mocking |
| FluentAssertions | 6.x | Readable assertions |
| TestContainers | 3.x | Real PostgreSQL and Redis in integration tests |
| Microsoft.AspNetCore.Mvc.Testing | 10 | WebApplicationFactory for integration tests |
| Bogus | 35.x | Fake data generation for tests |

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
| seq | datalust/seq | 5341 / 80 |
| prometheus | prom/prometheus | 9090 |
| grafana | grafana/grafana | 3000 |
| jaeger | jaegertracing/all-in-one | 16686 / 4317 |

---

## NuGet Packages — Summary by Project

### Ecommerce.Domain
```xml
<!-- No external dependencies -->
```

### Ecommerce.Application
```xml
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />
```

### Ecommerce.Infrastructure
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
<PackageReference Include="EFCore.NamingConventions" Version="9.*" />
<PackageReference Include="Dapper" Version="2.*" />
<PackageReference Include="Npgsql" Version="8.*" />
<PackageReference Include="StackExchange.Redis" Version="2.*" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.*" />
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.Seq" Version="8.*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.*" />
```

### Ecommerce.API
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />
<PackageReference Include="Scalar.AspNetCore" Version="2.*" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Jaeger" Version="1.*" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.*" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="8.*" />
```

### Ecommerce.UnitTests & Ecommerce.IntegrationTests
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="Testcontainers.PostgreSql" Version="3.*" />
<PackageReference Include="Testcontainers.Redis" Version="3.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
<PackageReference Include="Bogus" Version="35.*" />
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
JAEGER_ENDPOINT=http://jaeger:4317
ADMIN_EMAIL=admin@ecommerce.com
ADMIN_PASSWORD=<strong password>
```

---

## References
- [GUARDRAILS.md](../GUARDRAILS.md)
- [ARCHITECTURE.md](./ARCHITECTURE.md)
- [CONVENTIONS.md](./CONVENTIONS.md)