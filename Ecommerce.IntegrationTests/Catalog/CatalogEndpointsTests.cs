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
using Xunit;

namespace Ecommerce.IntegrationTests.Catalog;

[Collection("CatalogEndpoints")]
public class CatalogEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public CatalogEndpointsTests(TestContainersFixture containers)
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

    private static async Task<Guid> CreateCategoryDirectAsync(CustomWebApplicationFactory factory, string name, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = Category.Create(name, slug);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Guid> CreateProductDirectAsync(
        CustomWebApplicationFactory factory, Guid categoryId, string name, string slug, decimal price = 29.90m, int stock = 10)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = Product.Create(name, "description", slug, price, stock, "https://example.com/img.png", categoryId);
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

    // AC-CAT-I01
    [Fact]
    public async Task Should_Return_200_When_GetProducts_With_No_Filters()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Shoes", "shoes");
        await CreateProductDirectAsync(factory, categoryId, "Running Shoes", "running-shoes");

        var response = await client.GetAsync("/api/v1/catalog/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-CAT-I02
    [Fact]
    public async Task Should_Return_200_With_Filtered_Products_When_GetProducts_With_CategorySlug()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Hats", "hats");
        await CreateProductDirectAsync(factory, categoryId, "Cap", "cap");

        var response = await client.GetAsync("/api/v1/catalog/products?category_slug=hats");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    // AC-CAT-I03
    [Fact]
    public async Task Should_Return_200_With_InStock_Products_When_GetProducts_With_InStockFilter()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Gloves", "gloves");
        await CreateProductDirectAsync(factory, categoryId, "Winter Gloves", "winter-gloves", stock: 5);
        await CreateProductDirectAsync(factory, categoryId, "Sold Out Gloves", "sold-out-gloves", stock: 0);

        var response = await client.GetAsync("/api/v1/catalog/products?in_stock=true&category_slug=gloves");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("in_stock").GetBoolean());
    }

    // AC-CAT-I04
    [Fact]
    public async Task Should_Return_400_When_GetProducts_With_PageSize_Above_100()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.GetAsync("/api/v1/catalog/products?page_size=200");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-CAT-I05
    [Fact]
    public async Task Should_Return_200_When_GetProductBySlug_With_Existing_Slug()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Belts", "belts");
        await CreateProductDirectAsync(factory, categoryId, "Leather Belt", "leather-belt");

        var response = await client.GetAsync("/api/v1/catalog/products/leather-belt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-CAT-I06
    [Fact]
    public async Task Should_Return_404_When_GetProductBySlug_With_NonExisting_Slug()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.GetAsync("/api/v1/catalog/products/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-CAT-I07
    [Fact]
    public async Task Should_Return_404_When_GetProductBySlug_With_Deleted_Product()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Socks", "socks");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Wool Socks", "wool-socks");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Set<Product>().FirstAsync(p => p.Id == productId);
            product.SoftDelete();
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/catalog/products/wool-socks");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-CAT-I08
    [Fact]
    public async Task Should_Return_200_When_GetCategories()
    {
        var (factory, client) = await CreateClientAsync();
        await CreateCategoryDirectAsync(factory, "Jackets", "jackets");

        var response = await client.GetAsync("/api/v1/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-CAT-I09
    [Fact]
    public async Task Should_Return_Cached_Result_On_Second_GetProducts_Request()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Scarves", "scarves");
        await CreateProductDirectAsync(factory, categoryId, "Wool Scarf", "wool-scarf");

        var first = await client.GetAsync("/api/v1/catalog/products?category_slug=scarves");
        var firstBody = await first.Content.ReadAsStringAsync();

        // Mutate the DB directly without going through the invalidating command —
        // a cache hit must still return the original (now stale) snapshot.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Set<Product>().FirstAsync(p => p.Slug == "wool-scarf");
            product.Update("Renamed Scarf", "description", 29.90m, 10, "https://example.com/img.png");
            await db.SaveChangesAsync();
        }

        var second = await client.GetAsync("/api/v1/catalog/products?category_slug=scarves");
        var secondBody = await second.Content.ReadAsStringAsync();

        secondBody.Should().Be(firstBody);
    }

    // AC-CAT-I10
    [Fact]
    public async Task Should_Return_Updated_Data_When_GetProductBySlug_After_Put()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Bags", "bags");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Tote Bag", "tote-bag");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        await client.GetAsync("/api/v1/catalog/products/tote-bag"); // warm the cache

        await client.PutAsJsonAsync($"/api/v1/catalog/products/{productId}", new
        {
            name = "Tote Bag", description = "Updated description", price = 39.90m, stock = 20, image_url = "https://example.com/new.png"
        });

        var response = await client.GetAsync("/api/v1/catalog/products/tote-bag");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("description").GetString().Should().Be("Updated description");
    }

    // AC-CAT-I11
    [Fact]
    public async Task Should_Return_201_When_CreateProduct_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Watches", "watches");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            name = "Smart Watch", description = "desc", price = 199.90m, stock = 5,
            image_url = "https://example.com/watch.png", category_id = categoryId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // AC-CAT-I12
    [Fact]
    public async Task Should_Return_403_When_CreateProduct_As_Customer()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Sunglasses", "sunglasses");
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            name = "Aviators", description = "desc", price = 59.90m, stock = 5,
            image_url = "https://example.com/sg.png", category_id = categoryId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _ = factory;
    }

    // AC-CAT-I13
    [Fact]
    public async Task Should_Return_401_When_CreateProduct_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            name = "X", description = "desc", price = 1m, stock = 1, image_url = "url", category_id = Guid.NewGuid()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-CAT-I14
    [Fact]
    public async Task Should_Return_409_When_CreateProduct_With_Duplicate_Slug()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Wallets", "wallets");
        await CreateProductDirectAsync(factory, categoryId, "Leather Wallet", "leather-wallet");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            name = "Leather Wallet", description = "desc", slug = "leather-wallet", price = 49.90m, stock = 5,
            image_url = "https://example.com/w.png", category_id = categoryId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // AC-CAT-I15
    [Fact]
    public async Task Should_Return_200_When_UpdateProduct_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Backpacks", "backpacks");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Hiking Backpack", "hiking-backpack");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var response = await client.PutAsJsonAsync($"/api/v1/catalog/products/{productId}", new
        {
            name = "Hiking Backpack 40L", description = "desc", price = 89.90m, stock = 8, image_url = "https://example.com/b.png"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-CAT-I16
    [Fact]
    public async Task Should_Return_404_When_UpdateProduct_With_NonExisting_Id()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var response = await client.PutAsJsonAsync($"/api/v1/catalog/products/{Guid.NewGuid()}", new
        {
            name = "X", description = "desc", price = 1m, stock = 1, image_url = "url"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-CAT-I17
    [Fact]
    public async Task Should_Return_204_When_DeleteProduct_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Umbrellas", "umbrellas");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Compact Umbrella", "compact-umbrella");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var response = await client.DeleteAsync($"/api/v1/catalog/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // AC-CAT-I18
    [Fact]
    public async Task Should_Return_429_When_Public_Catalog_Rate_Limit_Exceeded()
    {
        var (_, client) = await CreateClientAsync();

        HttpResponseMessage response = null!;
        for (var i = 0; i < 201; i++)
        {
            response = await client.GetAsync("/api/v1/catalog/categories");
        }

        response.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
