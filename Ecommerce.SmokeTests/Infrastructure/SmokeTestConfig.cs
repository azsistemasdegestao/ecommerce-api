namespace Ecommerce.SmokeTests.Infrastructure;

internal static class SmokeTestConfig
{
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("SMOKE_API_BASE_URL") ?? "http://localhost:8080";

    public static string AdminEmail =>
        Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@ecommerce.com";

    public static string AdminPassword =>
        Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123456";
}
