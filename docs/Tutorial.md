# Tutorial: From SPEC to Shipped Code

## Who this is for

This document is a guided walkthrough of how a feature gets built in this codebase, from the moment it's just an idea to the moment it's merged with passing tests. It is **not** a replacement for `CLAUDE.md`, `docs/GUARDRAILS.md`, or the documents in `docs/context/` — those remain the authoritative reference for every rule. This tutorial is the *narrative*: it explains *why* the workflow is shaped the way it is, and walks through every layer using one real, recently-shipped feature as a running example.

That example is **product image upload**: before it existed, `Product.ImageUrl` was just a string field — whoever created a product had to already have the image hosted somewhere else and paste in the URL. The feature added in this codebase lets an Admin `POST` an actual image file to the API, which stores it in a self-hosted S3-compatible bucket (MinIO) and writes the resulting URL onto the product. It's a good teaching example because it touches every layer (Domain, Application, Infrastructure, API, tests) and forced several real tradeoffs, all of which are called out as we go and summarized in the [appendix](#appendix-full-tradeoff-ledger).

If you're about to build your own feature, you can use this as a step-by-step template. Each section ends with a **Tradeoffs** note — read those even if you skim the rest, because they explain the reasoning you're expected to reuse when you hit a similar decision.

---

## The Big Picture: Clean Architecture + CQRS + Event-Driven

The codebase has four layers, and dependencies only ever point inward:

```
┌─────────────────────────────────────────┐
│                   API                   │  ← HTTP entry, Minimal API, JWT, Rate Limiting
├─────────────────────────────────────────┤
│              Application                │  ← Use cases, Commands, Queries, Validators
├─────────────────────────────────────────┤
│                Domain                   │  ← Entities, Events, Interfaces, Business Rules
├─────────────────────────────────────────┤
│             Infrastructure              │  ← EF Core, Dapper, Redis, EventBus, Identity
└─────────────────────────────────────────┘
```

- **Domain** is the core. It has zero external dependencies — no EF Core, no HTTP, no AWS SDK. It only contains entities, value objects, repository/service *interfaces*, and domain events.
- **Application** orchestrates use cases with MediatR (Commands and Queries). It knows Domain, but nothing about how data is actually stored or served over HTTP.
- **Infrastructure** is where the real implementations live — EF Core repositories, Dapper queries, Redis, S3 clients, Identity. It's *injected* into Application/API at startup; Application and Domain never reference it directly.
- **API** is the outermost layer: Minimal API endpoints, JWT auth, rate limiting, middleware. It contains no business logic — it just translates HTTP into MediatR commands/queries and back.

Why this matters in practice: Domain defines `IImageStorageService` as an interface, but has no idea it's backed by MinIO. That means you could swap MinIO for real AWS S3, or even a local filesystem, by writing a new Infrastructure class — Domain and Application code wouldn't change at all.

### CQRS: two ways to talk to the database

| Operation | ORM | Layer |
|-----------|-----|-------|
| Commands (writes) | EF Core, via a repository | Application → Infrastructure |
| Queries (reads) | Dapper, via `IDbConnection` | Application → Infrastructure |

Writes go through EF Core because you want change tracking, transactions, and entity behavior (`product.UpdateImage(...)` is a method call on a tracked entity, not a raw SQL update). Reads go through Dapper because listing pages need fast, hand-tuned, paginated SQL projected directly into DTOs — never domain entities. Every listing query is paginated; `.ToList()` without pagination is not allowed anywhere in the codebase.

### Event-Driven: only where it earns its keep

Most of the system is request/response. The one place that's fully event-driven is **Payments**, because payment processing is inherently asynchronous: `POST /payments` dispatches a `RequestPaymentCommand`, which publishes a `PaymentRequested` event; a separate handler calls a mock gateway and publishes `PaymentProcessed` or `PaymentFailed` later. Events are *never* published from the API layer — only from Application, after persistence succeeds.

The image-upload feature is a small but useful illustration of "reuse the event system, don't grow it unnecessarily": when an image is uploaded, the handler publishes the **existing** `ProductUpdated` event — the same event already published by the regular product-update flow — instead of inventing a new `ProductImageUploaded` event. This means the existing `ProductUpdatedCacheHandler` (which invalidates the product's Redis cache entries) just works, for free.

**Tradeoffs**
- CQRS adds real complexity — two data-access technologies to learn and maintain — for endpoints that, today, don't need Dapper's raw performance (catalog listings aren't yet under heavy load). The project still applies it everywhere reads happen, in exchange for one consistent mental model instead of "sometimes EF Core, sometimes Dapper, depending on vibes."
- Reusing `ProductUpdated` instead of adding a new event got cache invalidation for free, but means any future event consumer can't tell "the image changed" apart from "any field changed." That's fine today because no consumer needs that distinction — if one ever does, that's the trigger to split the event, not before.

---

## Step 1 — Writing the SPEC

Nothing in this codebase is "real" until it's written down in `docs/specs/[feature]/SPEC-[feature].md`. The SPEC is the source of truth that both humans and the project's skills (`validate-spec`, `spec-to-tests`) read from — code is expected to match it, not the other way around.

Every SPEC follows the same shape (`docs/specs/catalog/SPEC-catalog.md` is the canonical example):

```
# SPEC-[feature].md
> Feature / Phase / Status

## Context              ← links to CONTEXT-*.md, GUARDRAILS, CONVENTIONS, DOMAIN-GLOSSARY
## Overview              ← one paragraph: what this feature does
## Endpoints              ← one ### block per route: Auth, Rate Limit, Cache, Request/Response, Errors
## Business Rules         ← numbered BR-[FEATURE]-NNN statements
## Domain Events          ← table: Event | Published when | Effect
## Validation Criteria
  ### Unit Tests          ← table: ID | Scenario | Input | Expected   (AC-[FEATURE]-Uxx)
  ### Integration Tests   ← table: ID | Scenario | Input | Expected   (AC-[FEATURE]-Ixx)
## Dependencies           ← table: Dependency | Type | Reason
## This feature is a dependency of
```

Event-driven features (like `SPEC-payments.md`) add one more section, a "Full Flow" diagram showing the event chain and any external simulation (the mock payment gateway's 80%/20% success/failure split) — otherwise the shape is identical.

**Worked example.** Adding image upload to the catalog meant editing `docs/specs/catalog/SPEC-catalog.md` in three places, *before* touching any code:

1. A new endpoint block, inserted between `POST /products` and `PUT /products/{id}`:
   ```
   ### POST /api/v1/catalog/products/{id}/image
   - Auth: JWT + Role Admin
   - Rate Limit: upload — 5 req/min
   Request: multipart/form-data, field file (jpeg/png/webp, max 5MB)
   Response 200 OK: { id, image_url, updated_at }
   Errors: 400 (invalid type/size), 404 (product not found)
   ```
2. Two new Business Rules: `BR-CAT-012` ("Image upload accepts only image/jpeg, image/png, image/webp, max 5MB") and `BR-CAT-013` ("`ProductUpdated` event reused after image upload, invalidates cache").
3. New rows in the Validation Criteria tables: `AC-CAT-U10`/`U11` (unit) and `AC-CAT-I19` through `I24` (integration) — each one a promise that a specific test will exist.

The `validate-spec` skill is the automated checkpoint that runs against a SPEC before implementation starts, checking it against `GUARDRAILS.md`/`CONVENTIONS.md`/`DOMAIN-GLOSSARY.md` so structural mistakes (missing sections, wrong ID format, undefined glossary terms) are caught on paper.

**Tradeoffs**
Writing the SPEC first is slower than "just start coding" — but the open questions for image upload (What's the max file size? Which content types? Do we delete the old image on replace? Does the legacy `image_url` field still work?) all got answered in the SPEC, on paper, before a single line of C# existed. Discovering those questions mid-implementation would have meant rewriting code, not just a markdown table.

---

## Step 2 — Domain layer

Domain code has zero external dependencies. For this feature, that meant two small, deliberate additions:

A new interface — the *port* that Infrastructure will later implement — added to `Ecommerce.Domain/Interfaces/IImageStorageService.cs`:

```csharp
public interface IImageStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string imageUrl, CancellationToken ct = default);
}
```

And a new method on the `Product` entity, alongside the existing full `Update(...)`:

```csharp
public void UpdateImage(string imageUrl)
{
    ImageUrl = imageUrl;
    UpdateTimestamp();
}
```

Notice `IImageStorageService` says nothing about S3, MinIO, buckets, or HTTP — just "upload a stream, get back a URL." Domain doesn't know or care what's on the other side.

**Tradeoffs**
Defining the storage interface in Domain — rather than just writing the S3 client directly in Application or Infrastructure — is the classic ports-and-adapters tradeoff: it's one more file and one more layer of indirection, in exchange for Application code that's testable with a mock and swappable to any storage backend without touching a single Application or Domain file.

---

## Step 3 — Application layer (CQRS in practice)

New features follow a fixed file structure:

```
Application/[Feature]/Commands/[Action][Entity]/
  [Action][Entity]Command.cs
  [Action][Entity]Handler.cs
  [Action][Entity]Validator.cs
  [Action][Entity]Response.cs
```

**Worked example.** `Ecommerce.Application/Catalog/Commands/UploadProductImage/` has all four files:

```csharp
// UploadProductImageCommand.cs
public sealed record UploadProductImageCommand(
    Guid ProductId, Stream FileStream, string FileName, string ContentType, long FileSize)
    : IRequest<UploadProductImageResponse>;
```

The command takes a raw `Stream` and primitives instead of `IFormFile` — `IFormFile` is an ASP.NET Core type, and Application is not allowed to know about ASP.NET Core. The API layer does the conversion (see Step 5).

```csharp
// UploadProductImageValidator.cs
RuleFor(x => x.ContentType).Must(ct => AllowedContentTypes.Contains(ct))...   // jpeg/png/webp only
RuleFor(x => x.FileSize).GreaterThan(0).LessThanOrEqualTo(5 * 1024 * 1024)... // max 5MB
```

This runs automatically through the existing `ValidationBehavior` pipeline, turning into a `400` via `ErrorHandlingMiddleware` — no manual validation code in the handler.

```csharp
// UploadProductImageHandler.cs
public async Task<UploadProductImageResponse> Handle(UploadProductImageCommand request, CancellationToken ct)
{
    var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
        ?? throw new NotFoundException("Product not found.");

    var imageUrl = await _imageStorageService.UploadAsync(
        request.FileStream, request.FileName, request.ContentType, ct);

    product.UpdateImage(imageUrl);
    _productRepository.Update(product);
    await _productRepository.SaveChangesAsync(ct);

    await _eventBus.PublishAsync(new ProductUpdated(Guid.NewGuid(), DateTime.UtcNow, product.Id, product.Slug), ct);

    return new UploadProductImageResponse(product.Id, product.ImageUrl, product.UpdatedAt);
}
```

Two details worth noticing: the product is loaded — and the 404 thrown — **before** anything is uploaded, so a request for a non-existing product never touches the storage bucket at all. And as mentioned earlier, the handler republishes `ProductUpdated` rather than a brand-new event.

**Tradeoffs**
Loading the product first means one extra round-trip to the database before the upload, but it guarantees a failed lookup never burns an upload to storage that would just be thrown away — a deliberate "fail fast, fail cheap" ordering.

---

## Step 4 — Infrastructure layer

Infrastructure is where interfaces from Domain finally get a concrete, technology-specific implementation. It's also the only layer allowed to reference EF Core, Dapper, Redis, or — in this case — the AWS S3 SDK.

**Worked example: `S3ImageStorageService`** implements `IImageStorageService` using `AWSSDK.S3`, pointed at MinIO instead of real AWS via `ServiceURL` + `ForcePathStyle = true`:

```csharp
public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
{
    var key = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
    await _s3Client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = _bucketName, Key = key, InputStream = fileStream, ContentType = contentType
    }, ct);
    return $"{_publicUrlBase}/{_bucketName}/{key}";
}
```

`DeleteAsync` checks that the URL actually starts with this service's own bucket prefix before attempting a delete — a legacy, externally-hosted `image_url` (the kind that predates this feature) is simply skipped with a structured warning log, never an exception.

**`BucketInitializer`** (same static-class style as the existing `RoleSeeder`) runs once at startup: it creates the bucket if missing and applies a public-read bucket policy, so every `image_url` is directly browser-accessible with no extra auth step:

```csharp
if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName))
    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });

await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = bucketName, Policy = publicReadPolicy });
```

A `StorageHealthCheck` (same `IHealthCheck` pattern as the existing `EventBusHealthCheck`) was added so `/health` reports MinIO connectivity alongside Postgres and Redis. Both `IAmazonS3` and `IImageStorageService` are registered in `InfrastructureExtensions`, read from configuration the same way `DB_CONNECTION_STRING` already is — fail fast at startup if a required `MINIO_*` variable is missing, rather than failing later on the first upload request.

**Tradeoffs**
- Using the generic `AWSSDK.S3` package against MinIO's S3-compatible API (instead of a MinIO-specific client library) means slightly more verbose configuration (`ServiceURL`, `ForcePathStyle`), but it makes a future move to real AWS S3 or Cloudflare R2 a config change, not a code change.
- The bucket is public with a direct URL — no presigned/expiring links. That's simpler and matches how product photos already worked in the catalog (they were already public URLs supplied by the client), but it means there's no access control or expiry on uploaded images. Acceptable for product photos; would not be acceptable for, say, private user documents.

---

## Step 5 — API layer

Every endpoint in this codebase needs `WithName`, `WithSummary`, `WithDescription`, `Produces` for each status code, a rate-limit policy, and (where relevant) an authorization policy. All JSON is `snake_case`, configured globally — handlers never have to think about casing.

**Worked example**, in `CatalogEndpoints.cs`:

```csharp
group.MapPost("/products/{id:guid}/image", UploadProductImage)
    .WithName("UploadProductImage")
    .WithSummary("Upload a product image")
    .WithDescription("Uploads an image (jpeg/png/webp, max 5MB) for an existing product, restricted to Admins.")
    .DisableAntiforgery()
    .Produces<UploadProductImageResponse>(StatusCodes.Status200OK)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
    .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
    .RequireAuthorization(policy => policy.RequireRole("Admin"))
    .RequireRateLimiting("upload");
```

```csharp
private static async Task<IResult> UploadProductImage(Guid id, IFormFile file, ISender sender, CancellationToken ct)
{
    await using var stream = file.OpenReadStream();
    var command = new UploadProductImageCommand(id, stream, file.FileName, file.ContentType, file.Length);
    var result = await sender.Send(command, ct);
    return Results.Ok(result);
}
```

This is exactly where `IFormFile` gets unpacked into the plain `Stream`/primitives the Application layer accepts. `.DisableAntiforgery()` is required on any minimal-API endpoint that binds form data — without it, .NET rejects the request with an unrelated-looking 400 before your code ever runs.

A new `upload` rate-limit policy was added in `RateLimitingExtensions.cs`, a sliding window of 5 requests/minute — stricter than the existing `payment` policy (10/min), because uploading a file is a heavier I/O operation and the endpoint is admin-only:

```csharp
options.AddSlidingWindowLimiter("upload", o =>
{
    o.Window = TimeSpan.FromMinutes(1);
    o.PermitLimit = 5;
    o.SegmentsPerWindow = 6;
});
```

**Tradeoffs**
`CreateProduct`/`UpdateProduct` still accept `image_url` as free text, unchanged. Upload is purely additive — a second way to set the same field — rather than a replacement. That trades a small amount of "two paths to the same outcome" inconsistency for zero breaking changes to any existing client that already sends `image_url` directly.

---

## Step 6 — Tests

Two test projects, two different jobs:

- **`Ecommerce.UnitTests`** — Moq + FluentAssertions, no real infrastructure, fast. Naming convention: `Should_[Result]_When_[Condition]`.
- **`Ecommerce.IntegrationTests`** — real PostgreSQL, Redis (and now MinIO) via TestContainers, through `CustomWebApplicationFactory`, exercising the actual HTTP pipeline.

**Worked example.** `UploadProductImageHandlerTests` mocks `IProductRepository`, `IImageStorageService`, and `IEventBus` directly — it proves the handler's branching logic (success path publishes `ProductUpdated`; missing product throws `NotFoundException` *and* never calls `UploadAsync`) without touching a network or a disk. `UploadProductImageEndpointTests`, by contrast, spins up a real MinIO container via the `Testcontainers.Minio` package (the same family already used for `Testcontainers.PostgreSql`/`Testcontainers.Redis`) and asserts the full round trip: a real multipart POST through real auth/rate-limiting middleware, a real object landing in a real bucket, and the product's `image_url` actually persisted and fetchable afterward.

Both are needed for different reasons: the unit test runs in milliseconds and pins down every logic branch; the integration test is the only one that would catch, say, a wrong `ServiceURL` or a misconfigured bucket policy.

One ripple effect worth knowing about: because `AddInfrastructure` throws if any required `MINIO_*` configuration value is missing (the same fail-fast pattern used for `DB_CONNECTION_STRING`), adding this *new, required* config meant every existing `CustomWebApplicationFactory` instantiation across the integration test suite — not just the new test file — had to be updated to also supply MinIO settings. That's the cost of "fail fast on missing config" as a project-wide rule: it catches misconfiguration immediately, but a new required dependency is never free to add.

Also worth knowing: `Ecommerce.IntegrationTests/xunit.runner.json` sets `parallelizeTestCollections: false`. Each integration test class spins up its own Postgres+Redis(+MinIO) containers; running test classes in parallel caused flaky connection-timeout failures under load. If integration tests fail in a full run but pass when filtered to a single class, suspect this setting before suspecting a real bug.

The `spec-to-tests` skill exists specifically to turn a SPEC's Validation Criteria tables into starter xUnit test files, so the SPEC and the test suite never drift apart by accident.

**Tradeoffs**
`parallelizeTestCollections: false` trades a slower full test-suite run for reliable, non-flaky integration tests — worth it because a flaky test that "sometimes fails" erodes trust in the whole suite faster than a slow one does.

---

## Step 7 — Closing the loop

A feature isn't done when the code compiles — it's done when the SPEC's promises are kept:

1. Every row added to the SPEC's Validation Criteria tables (Step 1) needs a real test with a matching scenario — `AC-CAT-U10`/`U11` and `AC-CAT-I19`–`I24` all map 1:1 to the tests written in Step 6. The SPEC is the contract; the tests are how you prove you honored it.
2. Local dev infrastructure needs to catch up: `docker-compose.yml` got a new `minio` service (with `healthcheck` and `service_healthy` as a startup dependency for `ecommerce-api`, the same pattern already used for `postgres`/`redis`), a new `minio-data` volume, and `.env.example` got the new `MINIO_*` variables.
3. This is also exactly where a real deployment would diverge from local dev: swapping `MINIO_ENDPOINT`/`MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` for real AWS S3 credentials and endpoint is a configuration-only change — none of the Domain/Application/Infrastructure/API code from Steps 2–5 needs to change, because `IImageStorageService` was the seam that made this possible.

---

## Summary: the checklist for your next feature

1. Write or extend the SPEC in `docs/specs/[feature]/SPEC-[feature].md` — Overview, Endpoints, Business Rules, Domain Events, Validation Criteria, Dependencies.
2. Run (or mentally apply) the `validate-spec` skill against it.
3. Domain: add/extend entities, value objects, and any new interface (port) the feature needs.
4. Application: scaffold `Commands/[Action][Entity]/{Command,Handler,Validator,Response}` (or `Queries/Get[Entity]`), following `docs/context/CONVENTIONS.md` naming.
5. Infrastructure: implement any new interface, register it in the relevant `*Extensions.cs`, add a health check if it's a new external dependency.
6. API: add the Minimal API endpoint with full `WithName/WithSummary/WithDescription/Produces`, an authorization policy if needed, and a rate-limit policy.
7. Tests: write unit tests for handler logic and validators; write/extend integration tests for the full HTTP path. Use `spec-to-tests` to generate skeletons from the SPEC's Validation Criteria.
8. Update `docker-compose.yml`/`.env.example` if a new external service or config value was introduced.
9. Go back to the SPEC and confirm every Validation Criteria row has a corresponding, passing test — that's what makes the feature actually "done."

---

## Appendix: full tradeoff ledger

| Decision | Why, and what we gave up |
|---|---|
| CQRS split (EF Core writes / Dapper reads) | Write consistency + tuned read performance, at the cost of two data-access paths to learn and maintain — applied everywhere for one consistent mental model, even where current load doesn't yet need it. |
| `IImageStorageService` defined in Domain | Domain/Application stay completely storage-agnostic and testable with a mock, at the cost of one extra interface/indirection layer. |
| Reused `ProductUpdated` event instead of a new event | Cache invalidation works for free via the existing handler, at the cost of event consumers being unable to distinguish "image changed" from "any field changed." |
| Loading the product (and 404-ing) before uploading the file | Guarantees a failed lookup never wastes a storage write, at the cost of one extra DB round-trip before any upload happens. |
| `AWSSDK.S3` against MinIO, not a MinIO-specific SDK | A future swap to real AWS S3/Cloudflare R2 is a config-only change, at the cost of slightly more generic/verbose client configuration (`ServiceURL`, `ForcePathStyle`). |
| Public bucket, direct URL (no presigned/expiring URLs) | Matches how product photos already worked (public URLs), and is simpler to implement, at the cost of no access control or expiry on uploaded images. |
| No old-image deletion on replace (v1) | Upload success is never coupled to a delete failure, at the cost of orphaned files accumulating in the bucket (cleanup is a future job, out of scope today). |
| Upload limits: 5MB, jpeg/png/webp only | Predictable storage costs and safe, well-understood content types, at the cost of no support for larger files or other image formats. |
| `image_url` free text kept alongside the new upload endpoint | Zero breaking changes for any existing client, at the cost of two different ways to end up setting the same field. |
| Reusing `MINIO_ROOT_USER`/`MINIO_ROOT_PASSWORD` as the app's S3 credentials | Simple local-dev setup, at the cost of not being production-grade — a real deployment needs scoped IAM credentials instead of root credentials. |
| Mandatory `MINIO_*` configuration (fail-fast on missing config) | Misconfiguration is caught immediately at startup, at the cost of a ripple change across every existing integration test factory when the new required config was introduced. |
| `parallelizeTestCollections: false` for integration tests | Avoids flaky Testcontainer connection-timeout failures, at the cost of a slower full integration test run. |
