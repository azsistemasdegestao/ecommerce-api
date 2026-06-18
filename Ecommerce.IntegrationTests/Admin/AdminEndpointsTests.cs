using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Ecommerce.Domain.Entities;
using Ecommerce.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ecommerce.IntegrationTests.Admin;

[Collection("AdminEndpoints")]
public class AdminEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public AdminEndpointsTests(TestContainersFixture containers)
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

    private static async Task<Guid> CreateUserAsync(
        CustomWebApplicationFactory factory, string email, string password = "Password@123", string role = "Customer")
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            CreatedAt = now,
            UpdatedAt = now
        };

        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, role);

        return user.Id;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password = "Password@123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    // AC-ADMIN-I01
    [Fact]
    public async Task Should_Return_200_When_GetUsers_As_Admin()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, adminEmail, role: "Admin");
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ADMIN-I02
    [Fact]
    public async Task Should_Return_403_When_GetUsers_As_Customer()
    {
        var (factory, client) = await CreateClientAsync();
        var customerEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, customerEmail, role: "Customer");
        Authorize(client, await LoginAsync(client, customerEmail));

        var response = await client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // AC-ADMIN-I03
    [Fact]
    public async Task Should_Return_401_When_GetUsers_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();

        var response = await client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-ADMIN-I04
    [Fact]
    public async Task Should_Return_200_When_GetUserById_With_Existing_Id()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        var adminId = await CreateUserAsync(factory, adminEmail, role: "Admin");
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.GetAsync($"/api/v1/admin/users/{adminId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ADMIN-I05
    [Fact]
    public async Task Should_Return_404_When_GetUserById_With_NonExisting_Id()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, adminEmail, role: "Admin");
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // AC-ADMIN-I06
    [Fact]
    public async Task Should_Return_204_When_DeactivateUser_With_Valid_Id()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, adminEmail, role: "Admin");
        var targetId = await CreateUserAsync(factory, _faker.Internet.Email());
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.DeleteAsync($"/api/v1/admin/users/{targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // AC-ADMIN-I07
    [Fact]
    public async Task Should_Return_400_When_DeactivateUser_Targets_Self()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        var adminId = await CreateUserAsync(factory, adminEmail, role: "Admin");
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.DeleteAsync($"/api/v1/admin/users/{adminId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-ADMIN-I08
    [Fact]
    public async Task Should_Return_200_When_UnlockUser_With_Active_Lockout()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, adminEmail, role: "Admin");
        var targetEmail = _faker.Internet.Email();
        var targetId = await CreateUserAsync(factory, targetEmail);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(targetId.ToString());
            await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(15));
        }

        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.PostAsync($"/api/v1/admin/users/{targetId}/unlock", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-ADMIN-I09
    [Fact]
    public async Task Should_Return_200_When_AssignRole_With_Valid_Role()
    {
        var (factory, client) = await CreateClientAsync();
        var adminEmail = _faker.Internet.Email();
        await CreateUserAsync(factory, adminEmail, role: "Admin");
        var targetId = await CreateUserAsync(factory, _faker.Internet.Email());
        Authorize(client, await LoginAsync(client, adminEmail));

        var response = await client.PostAsJsonAsync($"/api/v1/admin/users/{targetId}/roles", new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
