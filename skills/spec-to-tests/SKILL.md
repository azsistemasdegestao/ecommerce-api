# SKILL: spec-to-tests
> Generates xUnit test files from the Validation Criteria of a SPEC.md.

---

## Objective

Read a `SPEC-*.md` and its corresponding `CONTEXT-*.md` and generate:
- **Unit Tests** file in `Ecommerce.UnitTests/[Feature]/`
- **Integration Tests** file in `Ecommerce.IntegrationTests/[Feature]/`

---

## Documents this skill reads (required)

```
1. docs/GUARDRAILS.md
2. docs/context/ARCHITECTURE.md
3. docs/context/CONVENTIONS.md
4. docs/context/TECH-STACK.md
5. docs/specs/[feature]/SPEC-[feature].md
6. docs/specs/[feature]/CONTEXT-[feature].md
```

---

## Input

```
feature: auth | admin | catalog | cart | orders | payments
```

---

## Output

```
tests/
  Ecommerce.UnitTests/[Feature]/
    [Handler]Tests.cs
  Ecommerce.IntegrationTests/[Feature]/
    [Feature]EndpointsTests.cs
```

---

## Generation Rules

### 1. Single source of truth
- Generate **only** tests based on the `Validation Criteria` table of the SPEC.
- Never invent scenarios not listed in the table.
- Each row of `Unit Tests` → one `[Fact]` method in the unit test file.
- Each row of `Integration Tests` → one `[Fact]` method in the integration test file.

### 2. Required naming
```csharp
// Pattern: Should_[Result]_When_[Condition]
// Derived from the "Scenario" column of the Validation Criteria table
Should_Return_401_When_Password_Is_Wrong()
Should_Return_JWT_When_Login_Is_Valid()
```

### 3. Required AAA structure
```csharp
[Fact]
public async Task Should_[Result]_When_[Condition]()
{
    // Arrange
    // Act
    // Assert
}
```

### 4. Test stack
```csharp
// Unit Tests
using Moq;
using FluentAssertions;
using Xunit;
using Bogus;

// Integration Tests
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using FluentAssertions;
using Xunit;
```

### 5. Unit Tests — mock pattern
```csharp
private static readonly Faker _faker = new();
private readonly Mock<IOrderRepository> _repositoryMock = new();
private readonly Mock<IEventBus> _eventBusMock = new();
private readonly CreateOrderHandler _handler;

public CreateOrderHandlerTests()
{
    _handler = new CreateOrderHandler(
        _repositoryMock.Object,
        _eventBusMock.Object);
}
```

### 6. Integration Tests — WebApplicationFactory pattern
```csharp
public class AuthEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                        ["ConnectionStrings:Redis"] = _redis.GetConnectionString()
                    });
                });
            });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await _factory.DisposeAsync();
    }
}
```

### 7. Required assertions by test type

**Unit — verify behavior:**
```csharp
result.Should().NotBeNull();
result.AccessToken.Should().NotBeNullOrEmpty();
_repositoryMock.Verify(x => x.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
_eventBusMock.Verify(x => x.PublishAsync(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()), Times.Once);

var act = () => _handler.Handle(command, CancellationToken.None);
await act.Should().ThrowAsync<BusinessException>();
```

**Integration — verify HTTP:**
```csharp
response.StatusCode.Should().Be(HttpStatusCode.Created);
var body = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
body!.Id.Should().NotBeEmpty();
body.Status.Should().Be("Pending");
```

### 8. Fake data
```csharp
private static readonly Faker _faker = new();

var command = new RegisterUserCommand(
    FirstName: _faker.Name.FirstName(),
    LastName: _faker.Name.LastName(),
    Email: _faker.Internet.Email(),
    Password: "Password@123"
);
```

---

## Example Output — Unit Test

```csharp
// tests/Ecommerce.UnitTests/Auth/LoginHandlerTests.cs

// AC-AUTH-U05
[Fact]
public async Task Should_Return_JWT_When_Credentials_Are_Valid()
{
    // Arrange
    var command = new LoginCommand(_faker.Internet.Email(), "Password@123");
    _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
        .Returns("fake-jwt-token");

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.AccessToken.Should().NotBeNullOrEmpty();
    result.ExpiresIn.Should().Be(3600);
}
```

---

## Example Output — Integration Test

```csharp
// tests/Ecommerce.IntegrationTests/Auth/AuthEndpointsTests.cs

// AC-AUTH-I07
[Fact]
public async Task Should_Return_423_After_5_Failed_Login_Attempts()
{
    // Arrange
    var request = new { email = "user@test.com", password = "WrongPassword" };

    // Act — 5 attempts
    for (int i = 0; i < 5; i++)
        await _client.PostAsJsonAsync("/api/v1/auth/login", request);
    var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Locked);
}
```

---

## How to use this skill

```
Prompt:

"Read the following documents in order:
1. docs/GUARDRAILS.md
2. docs/context/ARCHITECTURE.md
3. docs/context/CONVENTIONS.md
4. docs/context/TECH-STACK.md
5. docs/specs/auth/SPEC-auth.md
6. docs/specs/auth/CONTEXT-auth.md

Using the spec-to-tests skill (docs/skills/spec-to-tests/SKILL.md),
generate the test files for the auth feature."
```

---

## Output validation checklist

- [ ] All Validation Criteria IDs have a corresponding method
- [ ] Naming follows `Should_[Result]_When_[Condition]`
- [ ] AAA structure present in all tests
- [ ] Mocks configured for all scenarios
- [ ] FluentAssertions used (never `Assert.Equal`)
- [ ] CancellationToken present in all async methods
- [ ] No test accesses real database in unit tests
- [ ] Integration tests use TestContainers
- [ ] Bogus used for fake data