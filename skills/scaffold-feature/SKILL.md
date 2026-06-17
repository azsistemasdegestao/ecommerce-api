# SKILL: scaffold-feature
> Generates the complete file structure and base code for a new feature.

---

## Objective

Read a `SPEC-*.md` and its `CONTEXT-*.md` and generate all files needed to implement the feature:
- Domain entities
- Application Commands, Queries, and Handlers
- Infrastructure repositories and query services
- API endpoints
- Test files (skeleton)

---

## Documents this skill reads (required)

```
1. docs/GUARDRAILS.md
2. docs/context/ARCHITECTURE.md
3. docs/context/CONVENTIONS.md
4. docs/context/TECH-STACK.md
5. docs/context/DOMAIN-GLOSSARY.md
6. docs/specs/[feature]/SPEC-[feature].md
7. docs/specs/[feature]/CONTEXT-[feature].md
```

---

## Generation Rules

### 1. Domain — Entities
```csharp
public sealed class [Entity] : BaseEntity, ISoftDelete
{
    public string [Prop] { get; private set; } = string.Empty;

    private [Entity]() { }

    public static [Entity] Create([params])
    {
        return new [Entity]
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void Update[Prop]([type] value)
    {
        [Prop] = value;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### 2. Domain — Events
```csharp
public sealed record [Entity][Action](
    Guid EventId,
    DateTime OccurredAt,
    // event data
) : IDomainEvent;
```

### 3. Application — Commands
```csharp
public sealed record [Action][Entity]Command(
    // properties
) : IRequest<[Action][Entity]Response>;

public sealed class [Action][Entity]Handler
    : IRequestHandler<[Action][Entity]Command, [Action][Entity]Response>
{
    public async Task<[Action][Entity]Response> Handle(
        [Action][Entity]Command request,
        CancellationToken cancellationToken)
    {
        // TODO: implement
        throw new NotImplementedException();
    }
}

public sealed class [Action][Entity]Validator
    : AbstractValidator<[Action][Entity]Command>
{
    public [Action][Entity]Validator()
    {
        // Rules based on SPEC Business Rules
    }
}
```

### 4. Application — Queries (Dapper)
```csharp
public sealed record Get[Entity]Query(
    // filter parameters
) : IRequest<PagedResponse<[Entity]Dto>>;

public sealed class Get[Entity]Handler
    : IRequestHandler<Get[Entity]Query, PagedResponse<[Entity]Dto>>
{
    public async Task<PagedResponse<[Entity]Dto>> Handle(
        Get[Entity]Query request,
        CancellationToken cancellationToken)
    {
        // TODO: implement cache-aside if applicable
        // TODO: call query service
        throw new NotImplementedException();
    }
}
```

### 5. Infrastructure — Repository
```csharp
public sealed class [Entity]Repository : I[Entity]Repository
{
    private readonly AppDbContext _context;
    public [Entity]Repository(AppDbContext context) => _context = context;

    public async Task<[Entity]?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.[Entities].FindAsync([id], ct);

    public async Task CreateAsync([Entity] entity, CancellationToken ct = default)
    {
        await _context.[Entities].AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }
}
```

### 6. Infrastructure — Query Service (Dapper)
```csharp
public sealed class [Entity]QueryService : I[Entity]QueryService
{
    private readonly IDbConnection _connection;
    public [Entity]QueryService(IDbConnection connection) => _connection = connection;

    public async Task<PagedResponse<[Entity]Dto>> GetAsync(Get[Entity]Query query, CancellationToken ct = default)
    {
        // SQL from CONTEXT-[feature].md
        var sql = """
            -- TODO: paste SQL from CONTEXT
        """;
        throw new NotImplementedException();
    }
}
```

### 7. API — Endpoints
```csharp
public static class [Feature]Endpoints
{
    public static void Map[Feature]Endpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/[feature]")
            .WithTags("[Feature]");

        group.MapPost("/", [Handler])
            .WithName("[Action][Entity]")
            .WithSummary("[Description from SPEC]")
            .Produces<[Response]>([StatusCode])
            .Produces<ProblemDetails>([ErrorCode])
            .RequireRateLimiting("[policy]");
    }
}
```

### 8. EF Core — Configuration
```csharp
public sealed class [Entity]Configuration : IEntityTypeConfiguration<[Entity]>
{
    public void Configure(EntityTypeBuilder<[Entity]> builder)
    {
        builder.ToTable("[snake_case_table]");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.[Prop]).IsRequired().HasMaxLength(NNN);
        builder.HasIndex(x => x.[SearchColumn]).IsUnique();
        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

---

## Important Rules

- [ ] **Never** implement business logic — generate skeleton only with `throw new NotImplementedException()`
- [ ] **Always** include `// TODO:` comments indicating what needs to be implemented
- [ ] **Always** include the SQL from CONTEXT as a comment in the QueryService
- [ ] **Always** register new services in DI (extension methods)
- [ ] **Always** use `CancellationToken` in async methods
- [ ] **Always** generate `sealed` on classes that should not be inherited

---

## How to use this skill

```
Prompt:

"Read the following documents in order:
1. docs/GUARDRAILS.md
2. docs/context/ARCHITECTURE.md
3. docs/context/CONVENTIONS.md
4. docs/context/TECH-STACK.md
5. docs/context/DOMAIN-GLOSSARY.md
6. docs/specs/catalog/SPEC-catalog.md
7. docs/specs/catalog/CONTEXT-catalog.md

Using the scaffold-feature skill (docs/skills/scaffold-feature/SKILL.md),
generate the complete scaffold for the catalog feature."
```

---

## Output validation checklist

- [ ] All files listed in CONTEXT were generated
- [ ] Entities with `private set` and `Create()` factory method
- [ ] Handlers with `throw new NotImplementedException()` in body
- [ ] Validators with rules based on SPEC Business Rules
- [ ] Endpoints with `Produces<>` for all SPEC status codes
- [ ] EF Core configurations with snake_case and indexes
- [ ] DI registrations in extension methods
- [ ] Tests with AAA skeleton and `// TODO: implement` methods