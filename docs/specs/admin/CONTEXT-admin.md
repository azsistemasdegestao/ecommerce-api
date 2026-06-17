# CONTEXT-admin.md
> Feature-specific context document for Admin.

---

## Authorization

All routes use:
```csharp
.RequireAuthorization(policy => policy.RequireRole("Admin"))
```

---

## Commands and Queries

### Commands
```
DeactivateUserCommand    → validates TargetUserId != RequestingAdminId
UnlockUserCommand        → validates user has active lockout
AssignRoleCommand        → validates Role in ["Admin", "Customer"]
                         → publishes: UserRoleAssigned
UpdateOrderStatusCommand → validates allowed transition
                         → publishes: OrderStatusUpdated
RefundPaymentCommand     → validates Payment.Status == Processed
                         → publishes: PaymentRefunded
CreateCategoryCommand    → auto-generates Slug if absent, validates unique
UpdateCategoryCommand    → validates CategoryId exists, unique Slug
DeleteCategoryCommand    → validates no active products linked
```

### Queries (Dapper)
```
GetUsersQuery         → JOIN: AspNetUsers + AspNetUserRoles + AspNetRoles
GetUserByIdQuery      → includes Role, IsLocked, LockoutEnd, FailedLoginAttempts
GetAllOrdersQuery     → JOIN: orders + users, filters by Status and UserId
GetOrderByIdAdminQuery→ JOIN: orders + order_items + users
GetAllPaymentsQuery   → JOIN: payments + orders + users
```

---

## Slug Generation

```csharp
// Ecommerce.Application/Common/Helpers/SlugHelper.cs
public static class SlugHelper
{
    public static string Generate(string name) =>
        name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ã", "a").Replace("â", "a").Replace("á", "a")
            // ... other replacements
            // remove non-alphanumeric (except hyphen)
}
```

---

## Seed Data

```csharp
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "Customer" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail, Email = adminEmail,
                FirstName = "Admin", LastName = "System",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(admin, adminPassword);
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
```

---

## File Structure

```
Ecommerce.Application/
  Admin/
    Commands/
      DeactivateUser/ UnlockUser/ AssignRole/
      UpdateOrderStatus/ RefundPayment/
      CreateCategory/ UpdateCategory/ DeleteCategory/
    Queries/
      GetUsers/ GetUserById/ GetAllOrders/
      GetOrderByIdAdmin/ GetAllPayments/
  Common/Helpers/SlugHelper.cs

Ecommerce.Infrastructure/
  Queries/AdminQueryService.cs
  Seeding/AdminSeeder.cs

Ecommerce.API/
  Endpoints/Admin/
    AdminUsersEndpoints.cs
    AdminOrdersEndpoints.cs
    AdminPaymentsEndpoints.cs
    AdminCategoriesEndpoints.cs

Ecommerce.UnitTests/Admin/
  DeactivateUserHandlerTests.cs / UnlockUserHandlerTests.cs
  AssignRoleHandlerTests.cs / UpdateOrderStatusHandlerTests.cs
  RefundPaymentHandlerTests.cs / CreateCategoryHandlerTests.cs
  DeleteCategoryHandlerTests.cs

Ecommerce.IntegrationTests/Admin/AdminEndpointsTests.cs
```

---

## References
- [SPEC-admin.md](./SPEC-admin.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [CONVENTIONS.md](../../context/CONVENTIONS.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)