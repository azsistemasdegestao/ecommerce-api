using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using Ecommerce.IntegrationTests.Infrastructure;
using Ecommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ecommerce.IntegrationTests.Admin;

[Collection("AdminPaymentsEndpoints")]
public class AdminPaymentsEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public AdminPaymentsEndpointsTests(TestContainersFixture containers)
    {
        _containers = containers;
    }

    private sealed class FakeGatewayService : IMockGatewayService
    {
        private readonly bool _success;
        public FakeGatewayService(bool success) => _success = success;

        public Task<GatewayResult> ProcessAsync(Guid paymentId, decimal amount, CancellationToken ct = default) =>
            Task.FromResult(_success ? new GatewayResult(true, null) : new GatewayResult(false, "Insufficient funds"));
    }

    private Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateClientAsync(bool? gatewaySuccess = null)
    {
        var factory = new CustomWebApplicationFactory
        {
            PostgresConnectionString = _containers.Postgres.GetConnectionString(),
            RedisConnectionString = _containers.Redis.GetConnectionString(),
            ConfigureTestServices = gatewaySuccess is null
                ? null
                : services =>
                {
                    services.RemoveAll<IMockGatewayService>();
                    services.AddScoped<IMockGatewayService>(_ => new FakeGatewayService(gatewaySuccess.Value));
                }
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

    private static async Task<Guid> CheckoutAndPayAsync(HttpClient client, CustomWebApplicationFactory factory, string seed)
    {
        var productId = await CreateProductDirectAsync(factory, seed, seed);
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var orderResponse = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });
        var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderBody.GetProperty("id").GetGuid();

        var paymentResponse = await client.PostAsJsonAsync("/api/v1/payments", new { order_id = orderId });
        var paymentBody = await paymentResponse.Content.ReadFromJsonAsync<JsonElement>();
        return paymentBody.GetProperty("payment_id").GetGuid();
    }

    // AC-ADMIN-I14
    [Fact]
    public async Task Should_Return_200_With_PaginatedList_When_GetAllPayments_As_Admin()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        await CheckoutAndPayAsync(client, factory, "admin-pay-tshirt");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.GetAsync("/api/v1/admin/payments");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    // AC-ADMIN-I15
    [Fact]
    public async Task Should_Return_200_With_Refunded_Status_When_Refunding_Processed_Payment()
    {
        var (factory, client) = await CreateClientAsync(gatewaySuccess: true);
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var paymentId = await CheckoutAndPayAsync(client, factory, "admin-pay-cap");

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.PostAsync($"/api/v1/admin/payments/{paymentId}/refund", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Refunded");
    }

    // AC-ADMIN-I16
    [Fact]
    public async Task Should_Return_422_When_Refunding_Pending_Payment()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var productId = await CreateProductDirectAsync(factory, "admin-pay-belt", "admin-pay-belt");
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var orderResponse = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });
        var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderBody.GetProperty("id").GetGuid();

        Guid paymentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var payment = Payment.Create(orderId, 29.90m, "MockGateway");
            db.Set<Payment>().Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        Authorize(client, await CreateAdminAndLoginAsync(factory, client));
        var response = await client.PostAsync($"/api/v1/admin/payments/{paymentId}/refund", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
