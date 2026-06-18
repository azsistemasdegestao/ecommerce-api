using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.Domain.Entities;
using Ecommerce.IntegrationTests.Infrastructure;
using Ecommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.IntegrationTests.Admin;

[Collection("AdminOrdersEndpoints")]
public class AdminOrdersEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public AdminOrdersEndpointsTests(TestContainersFixture containers)
    {
        _containers = containers;
    }

    private Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateClientAsync()
    {
        var factory = new CustomWebApplicationFactory
        {
            PostgresConnectionString = _containers.Postgres.GetConnectionString(),
            RedisConnectionString = _containers.Redis.GetConnectionString(),
            MinioEndpoint = _containers.Minio.GetConnectionString(),
            MinioAccessKey = _containers.Minio.GetAccessKey(),
            MinioSecretKey = _containers.Minio.GetSecretKey()
        };

        return Task.FromResult((factory, factory.CreateClient()));
    }

    private static async Task<Guid> CreateProductDirectAsync(
        CustomWebApplicationFactory factory, string name, string slug, decimal price = 29.90m, int stock = 10)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = Category.Create(_faker.Commerce.Department(), _faker.Lorem.Slug());
        db.Set<Category>().Add(category);
        var product = Product.Create(name, "description", slug, price, stock, "https://example.com/img.png", category.Id);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<string> CreateAdminAndLoginAsync(CustomWebApplicationFactory factory, HttpClient client)
    {
        var email = _faker.Internet.Email();
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

            var now = DateTime.UtcNow;
            var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Admin", LastName = "Test", CreatedAt = now, UpdatedAt = now };
            await userManager.CreateAsync(user, "Password@123");
            await userManager.AddToRoleAsync(user, "Admin");
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    private static async Task<string> CreateCustomerAndLoginAsync(HttpClient client)
    {
        var email = _faker.Internet.Email();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "Test", last_name = "Customer", email, password = "Password@123"
        });

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static async Task<Guid> CheckoutAsync(HttpClient client, CustomWebApplicationFactory factory, string seed)
    {
        var productId = await CreateProductDirectAsync(factory, seed, seed);
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    // AC-ADMIN-I10
    [Fact]
    public async Task Should_Return_200_With_PaginatedList_When_GetAllOrders_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        await CheckoutAsync(client, factory, "admin-orders-tshirt");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.GetAsync("/api/v1/admin/orders");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    // AC-ADMIN-I11
    [Fact]
    public async Task Should_Return_200_When_GetOrderById_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "admin-orders-cap");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.GetAsync($"/api/v1/admin/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ADMIN-I12
    [Fact]
    public async Task Should_Return_200_When_UpdateOrderStatus_With_Allowed_Transition()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "admin-orders-belt");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "Confirmed" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ADMIN-I13
    [Fact]
    public async Task Should_Return_422_When_UpdateOrderStatus_With_Forbidden_Transition()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "admin-orders-socks");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "Shipped" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
