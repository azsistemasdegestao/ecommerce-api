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

namespace Ecommerce.IntegrationTests.Cart;

[Collection("CartEndpoints")]
public class CartEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public CartEndpointsTests(TestContainersFixture containers)
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

    // AC-CART-I01
    [Fact]
    public async Task Should_Return_200_With_Empty_Cart_When_No_Items()
    {
        var (_, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.GetAsync("/api/v1/cart");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
        body.GetProperty("total").GetDecimal().Should().Be(0);
    }

    // AC-CART-I02
    [Fact]
    public async Task Should_Return_200_With_Items_And_Total_When_Cart_Has_Products()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Blue T-Shirt", "blue-t-shirt");
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 2 });

        var response = await client.GetAsync("/api/v1/cart");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().HaveCount(1);
        body.GetProperty("total").GetDecimal().Should().Be(59.80m);
    }

    // AC-CART-I03
    [Fact]
    public async Task Should_Return_401_When_GetCart_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.GetAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-CART-I04
    [Fact]
    public async Task Should_Return_201_When_AddItemToCart_With_Valid_Product()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Cap", "cap");
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // AC-CART-I05
    [Fact]
    public async Task Should_Return_404_When_AddItemToCart_With_NonExisting_Product()
    {
        var (_, client) = await CreateClientAsync();
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = Guid.NewGuid(), quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-CART-I06
    [Fact]
    public async Task Should_Return_422_When_AddItemToCart_Out_Of_Stock()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Sold Out Shoes", "sold-out-shoes", stock: 0);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-CART-I07
    [Fact]
    public async Task Should_Return_201_With_Summed_Quantity_When_AddItemToCart_Duplicate_Product()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Gloves", "gloves", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 2 });
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 3 });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.GetProperty("quantity").GetInt32().Should().Be(5);
    }

    // AC-CART-I08
    [Fact]
    public async Task Should_Return_200_When_UpdateCartItem_With_Valid_Quantity()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Backpack", "backpack", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var addResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = addBody.GetProperty("item_id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{itemId}", new { quantity = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-CART-I09
    [Fact]
    public async Task Should_Return_422_When_UpdateCartItem_Quantity_Greater_Than_Stock()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Umbrella", "umbrella", stock: 3);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var addResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = addBody.GetProperty("item_id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{itemId}", new { quantity = 10 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-CART-I10
    [Fact]
    public async Task Should_Return_403_When_UpdateCartItem_From_Another_User()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Wallet", "wallet", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var addResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = addBody.GetProperty("item_id").GetGuid();

        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{itemId}", new { quantity = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = factory;
    }

    // AC-CART-I11
    [Fact]
    public async Task Should_Return_204_When_DeleteCartItem_Existing()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Watch", "watch", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var addResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = addBody.GetProperty("item_id").GetGuid();

        var response = await client.DeleteAsync($"/api/v1/cart/items/{itemId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // AC-CART-I12
    [Fact]
    public async Task Should_Return_403_When_DeleteCartItem_From_Another_User()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Sunglasses", "sunglasses", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var addResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });
        var addBody = await addResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = addBody.GetProperty("item_id").GetGuid();

        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.DeleteAsync($"/api/v1/cart/items/{itemId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = factory;
    }

    // AC-CART-I13
    [Fact]
    public async Task Should_Return_204_When_ClearCart_With_Items()
    {
        var (factory, client) = await CreateClientAsync();
        var productId = await CreateProductDirectAsync(factory, "Hat", "hat", stock: 10);
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        await client.PostAsJsonAsync("/api/v1/cart/items", new { product_id = productId, quantity = 1 });

        var response = await client.DeleteAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/v1/cart");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
        _ = factory;
    }
}
