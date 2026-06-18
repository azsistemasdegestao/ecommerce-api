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
public class UploadProductImageEndpointTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public UploadProductImageEndpointTests(TestContainersFixture containers)
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
        CustomWebApplicationFactory factory, Guid categoryId, string name, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = Product.Create(name, "description", slug, 29.90m, 10, "https://example.com/img.png", categoryId);
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

    private static MultipartFormDataContent BuildFileContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    // AC-CAT-I19
    [Fact]
    public async Task Should_Return_200_When_UploadProductImage_As_Admin_With_Valid_File()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Lamps", "lamps");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Desk Lamp", "desk-lamp");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        using var content = BuildFileContent([1, 2, 3, 4], "lamp.jpg", "image/jpeg");
        var response = await client.PostAsync($"/api/v1/catalog/products/{productId}/image", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("image_url").GetString().Should().NotBeNullOrEmpty();
    }

    // AC-CAT-I20
    [Fact]
    public async Task Should_Return_404_When_UploadProductImage_With_NonExisting_Product()
    {
        var (factory, client) = await CreateClientAsync();
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        using var content = BuildFileContent([1, 2, 3, 4], "lamp.jpg", "image/jpeg");
        var response = await client.PostAsync($"/api/v1/catalog/products/{Guid.NewGuid()}/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-CAT-I21
    [Fact]
    public async Task Should_Return_400_When_UploadProductImage_With_Invalid_ContentType()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Rugs", "rugs");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Wool Rug", "wool-rug");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        using var content = BuildFileContent([1, 2, 3, 4], "rug.txt", "text/plain");
        var response = await client.PostAsync($"/api/v1/catalog/products/{productId}/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-CAT-I22
    [Fact]
    public async Task Should_Return_400_When_UploadProductImage_With_File_Exceeding_5MB()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Mirrors", "mirrors");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Wall Mirror", "wall-mirror");
        Authorize(client, await CreateAdminAndLoginAsync(factory, client));

        var oversized = new byte[6 * 1024 * 1024];
        using var content = BuildFileContent(oversized, "mirror.jpg", "image/jpeg");
        var response = await client.PostAsync($"/api/v1/catalog/products/{productId}/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-CAT-I23
    [Fact]
    public async Task Should_Return_403_When_UploadProductImage_As_Customer()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Curtains", "curtains");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Blackout Curtains", "blackout-curtains");
        Authorize(client, await CreateCustomerAndLoginAsync(client));

        using var content = BuildFileContent([1, 2, 3, 4], "curtains.jpg", "image/jpeg");
        var response = await client.PostAsync($"/api/v1/catalog/products/{productId}/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-CAT-I24
    [Fact]
    public async Task Should_Return_401_When_UploadProductImage_Without_Jwt()
    {
        var (factory, client) = await CreateClientAsync();
        var categoryId = await CreateCategoryDirectAsync(factory, "Vases", "vases");
        var productId = await CreateProductDirectAsync(factory, categoryId, "Ceramic Vase", "ceramic-vase");

        using var content = BuildFileContent([1, 2, 3, 4], "vase.jpg", "image/jpeg");
        var response = await client.PostAsync($"/api/v1/catalog/products/{productId}/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
