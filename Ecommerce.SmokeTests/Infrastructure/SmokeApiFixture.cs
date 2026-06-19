using System.Net.Http.Json;
using System.Text.Json;

namespace Ecommerce.SmokeTests.Infrastructure;

public sealed class SmokeApiFixture : IAsyncLifetime
{
    public HttpClient Client { get; } = new() { BaseAddress = new Uri(SmokeTestConfig.BaseUrl) };

    public string CustomerEmail { get; private set; } = string.Empty;
    public string CustomerPassword { get; } = "Smoke@Test123";
    public string CustomerAccessToken { get; private set; } = string.Empty;
    public string CustomerRefreshToken { get; private set; } = string.Empty;
    public string AdminAccessToken { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductSlug { get; private set; } = string.Empty;
    public Guid OutOfStockProductId { get; private set; }

    public async Task InitializeAsync()
    {
        CustomerEmail = $"smoke.{Guid.NewGuid():N}@smoketest.local";

        await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/register", new
        {
            first_name = "Smoke",
            last_name = "Tester",
            email = CustomerEmail,
            password = CustomerPassword
        });

        var loginResponse = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/login", new
        {
            email = CustomerEmail,
            password = CustomerPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        CustomerAccessToken = login.GetProperty("access_token").GetString()!;
        CustomerRefreshToken = login.GetProperty("refresh_token").GetString()!;

        var adminLoginResponse = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/login", new
        {
            email = SmokeTestConfig.AdminEmail,
            password = SmokeTestConfig.AdminPassword
        });

        if (!adminLoginResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Admin login failed ({(int)adminLoginResponse.StatusCode}). Set the ADMIN_EMAIL/ADMIN_PASSWORD " +
                "environment variables to match the admin seeded in the running API to enable the catalog/checkout smoke tests.");
        }

        var adminLogin = await adminLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        AdminAccessToken = adminLogin.GetProperty("access_token").GetString()!;

        var categoryResponse = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/categories",
            new { name = $"Smoke Category {Guid.NewGuid():N}" }, AdminAccessToken);
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonElement>();
        CategoryId = category.GetProperty("id").GetGuid();

        ProductSlug = $"smoke-product-{Guid.NewGuid():N}";
        var productResponse = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/catalog/products", new
        {
            name = "Smoke Test Sneaker",
            description = "Product created by the observability smoke test suite.",
            slug = ProductSlug,
            price = 49.90m,
            stock = 100,
            image_url = "https://example.com/smoke-sneaker.png",
            category_id = CategoryId
        }, AdminAccessToken);
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
        ProductId = product.GetProperty("id").GetGuid();

        var outOfStockResponse = await Client.SendJsonAsync(HttpMethod.Post, "/api/v1/catalog/products", new
        {
            name = "Smoke Test Out Of Stock Item",
            description = "Always out of stock, used to exercise the 422 path.",
            slug = $"smoke-out-of-stock-{Guid.NewGuid():N}",
            price = 19.90m,
            stock = 0,
            image_url = "https://example.com/smoke-empty.png",
            category_id = CategoryId
        }, AdminAccessToken);
        outOfStockResponse.EnsureSuccessStatusCode();
        var outOfStock = await outOfStockResponse.Content.ReadFromJsonAsync<JsonElement>();
        OutOfStockProductId = outOfStock.GetProperty("id").GetGuid();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Smoke API")]
public sealed class SmokeApiCollection : ICollectionFixture<SmokeApiFixture>;
