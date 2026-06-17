using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.Domain.Entities;
using Ecommerce.IntegrationTests.Infrastructure;
using Ecommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.IntegrationTests.Orders;

[Collection("OrdersEndpoints")]
public class OrdersEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public OrdersEndpointsTests(TestContainersFixture containers)
    {
        _containers = containers;
    }

    private Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateClientAsync()
    {
        var factory = new CustomWebApplicationFactory
        {
            PostgresConnectionString = _containers.Postgres.GetConnectionString(),
            RedisConnectionString = _containers.Redis.GetConnectionString()
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

    private static async Task<Guid> CheckoutAsync(HttpClient client, CustomWebApplicationFactory factory, string productSlugSeed, int quantity = 1)
    {
        var productId = await CreateProductDirectAsync(factory, productSlugSeed, productSlugSeed);
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity });
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    // AC-ORD-I01
    [Fact]
    public async Task Should_Return_201_When_CreateOrder_With_Valid_Cart()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var productId = await CreateProductDirectAsync(factory, "Running Shoes", "running-shoes");
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });

        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // AC-ORD-I02
    [Fact]
    public async Task Should_Return_422_When_CreateOrder_With_Empty_Cart()
    {
        var (_, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-ORD-I03
    [Fact]
    public async Task Should_Return_401_When_CreateOrder_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "123 Main St" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-ORD-I04
    [Fact]
    public async Task Should_Return_400_When_CreateOrder_Without_ShippingAddress()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var productId = await CreateProductDirectAsync(factory, "Hat", "hat");
        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });

        var response = await client.PostAsJsonAsync("/api/v1/orders", new { shipping_address = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-ORD-I05
    [Fact]
    public async Task Should_Return_200_With_PaginatedList_When_GetOrders()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        await CheckoutAsync(client, factory, "backpack");

        var response = await client.GetAsync("/api/v1/orders");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    // AC-ORD-I06
    [Fact]
    public async Task Should_Return_200_When_GetOrderById_Own_Order()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "umbrella");

        var response = await client.GetAsync($"/api/v1/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ORD-I07
    [Fact]
    public async Task Should_Return_403_When_GetOrderById_From_Another_Customer()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "scarf");

        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-ORD-I08
    [Fact]
    public async Task Should_Return_404_When_GetOrderById_NonExisting()
    {
        var (_, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-ORD-I09
    [Fact]
    public async Task Should_Return_200_When_CancelOrder_Pending()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "wallet");

        var response = await client.PostAsync($"/api/v1/orders/{orderId}/cancel", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    // AC-ORD-I10
    [Fact]
    public async Task Should_Return_422_When_CancelOrder_Delivered()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        var orderId = await CheckoutAsync(client, factory, "watch");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Set<Domain.Entities.Order>().Include(o => o.Items).FirstAsync(o => o.Id == orderId);
            order.ChangeStatus(Domain.Enums.OrderStatus.Confirmed);
            order.ChangeStatus(Domain.Enums.OrderStatus.Processing);
            order.ChangeStatus(Domain.Enums.OrderStatus.Shipped);
            order.ChangeStatus(Domain.Enums.OrderStatus.Delivered);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/api/v1/orders/{orderId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-ORD-I11
    [Fact]
    public async Task Should_Return_Empty_Cart_After_Checkout()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));
        await CheckoutAsync(client, factory, "gloves");

        var response = await client.GetAsync("/api/v1/cart");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }
}
