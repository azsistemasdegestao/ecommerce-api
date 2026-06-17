using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Infrastructure.Seeding;

public static class RoleSeeder
{
    private static readonly string[] Roles = ["Admin", "Customer"];

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            try
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
            catch (DbUpdateException)
            {
                // Lost a race with another concurrent seeding call (e.g. parallel test hosts
                // sharing one database) — the role now exists either way, so this is safe to ignore.
            }
        }
    }
}
