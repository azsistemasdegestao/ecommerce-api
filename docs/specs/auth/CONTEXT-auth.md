# CONTEXT-auth.md
> Feature-specific context document for Auth.
> Read by skills alongside SPEC-auth.md for code and test generation.

---

## Feature Responsibility

Manages the entire authentication and authorization lifecycle:
- User creation (`Customer`)
- Email/password authentication with JWT
- Token renewal and revocation
- Password recovery via mocked email

---

## Data Model

### ApplicationUser
Extends `IdentityUser<Guid>` with domain fields.

```csharp
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

### Identity Tables
```
AspNetUsers       ← ApplicationUser + custom fields
AspNetRoles       ← Admin, Customer
AspNetUserRoles   ← User ↔ Role relation
AspNetUserClaims  ← additional claims
AspNetUserTokens  ← Refresh Tokens and reset tokens
```

---

## Interfaces

```csharp
public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string resetToken,
        CancellationToken ct = default);
}
```

---

## Implementations (Infrastructure)

### TokenService
```
Ecommerce.Infrastructure/Auth/TokenService.cs
- GenerateAccessToken: generates JWT with claims (sub, email, role, jti)
- GenerateRefreshToken: Guid.NewGuid().ToString("N")
- HashRefreshToken: SHA256 of the plain token
```

### MockEmailService
```
Ecommerce.Infrastructure/Email/MockEmailService.cs
- Does not send real emails
- Logs reset token to Seq via ILogger
- Log level: Warning
- Format: "[MockEmail] Password reset token for {Email}: {Token}"
```

---

## Commands and Queries

### Commands
```
RegisterUserCommand   → Input: FirstName, LastName, Email, Password
                      → Output: RegisterUserResponse
                      → Publishes: UserRegistered

LoginCommand          → Input: Email, Password
                      → Output: LoginResponse (AccessToken, RefreshToken, ExpiresIn, TokenType)
                      → Publishes: UserLoggedIn

RefreshTokenCommand   → Input: RefreshToken (plain)
                      → Output: LoginResponse (new tokens)

LogoutCommand         → Input: RefreshToken, UserId (from JWT)
                      → Output: void

ForgotPasswordCommand → Input: Email
                      → Output: void (always, even if email doesn't exist)

ResetPasswordCommand  → Input: Email, Token, NewPassword
                      → Output: void
```

---

## Refresh Token Flow

```
1. Successful login:
   - Generates plain RefreshToken: Guid.NewGuid().ToString("N")
   - Generates SHA256 hash of the token
   - Saves hash in AspNetUserTokens:
       LoginProvider: "EcommerceApi"
       Name: "RefreshToken"
       Value: token hash
   - Returns plain token to client

2. Refresh:
   - Receives plain token
   - Generates SHA256 hash
   - Looks up in AspNetUserTokens by hash
   - Validates expiration (7 days from CreatedAt)
   - Generates new token pair
   - Removes old token, saves new hash

3. Logout:
   - Removes user's AspNetUserTokens entry

4. Password reset:
   - Removes ALL user's RefreshTokens
```

---

## Identity Configuration

```csharp
services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
```

---

## JWT Configuration

```csharp
// Environment variables
JWT_SECRET    → secret key (minimum 32 chars)
JWT_ISSUER    → "ecommerce-api"
JWT_AUDIENCE  → "ecommerce-client"

// Generated claims
new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
new Claim(JwtRegisteredClaimNames.Email, user.Email)
new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
new Claim(ClaimTypes.Role, role)  // "Admin" or "Customer"

// Expiration
AccessToken:  DateTime.UtcNow.AddHours(1)
RefreshToken: DateTime.UtcNow.AddDays(7)
```

---

## File Structure

```
Ecommerce.Domain/
  Entities/ApplicationUser.cs
  Events/UserRegistered.cs / UserLoggedIn.cs
  Interfaces/ITokenService.cs / IEmailService.cs

Ecommerce.Application/
  Auth/
    Commands/
      RegisterUser/ Login/ RefreshToken/ Logout/ ForgotPassword/ ResetPassword/

Ecommerce.Infrastructure/
  Auth/TokenService.cs
  Email/MockEmailService.cs
  Persistence/Configurations/ApplicationUserConfiguration.cs

Ecommerce.API/
  Endpoints/Auth/AuthEndpoints.cs

Ecommerce.UnitTests/
  Auth/
    RegisterUserHandlerTests.cs / LoginHandlerTests.cs
    RefreshTokenHandlerTests.cs / LogoutHandlerTests.cs
    ForgotPasswordHandlerTests.cs / ResetPasswordHandlerTests.cs
    TokenServiceTests.cs

Ecommerce.IntegrationTests/
  Auth/AuthEndpointsTests.cs
```

---

## Seed Data

```csharp
// Roles created on migration or startup
await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));
await roleManager.CreateAsync(new IdentityRole<Guid>("Customer"));

// First Admin via environment variables: ADMIN_EMAIL and ADMIN_PASSWORD
```

---

## References
- [SPEC-auth.md](./SPEC-auth.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [TECH-STACK.md](../../context/TECH-STACK.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [DOMAIN-GLOSSARY.md](../../context/DOMAIN-GLOSSARY.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)