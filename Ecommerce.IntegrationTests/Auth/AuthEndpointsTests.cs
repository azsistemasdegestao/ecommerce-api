using System.Net;
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

namespace Ecommerce.IntegrationTests.Auth;

[Collection("AuthEndpoints")]
public class AuthEndpointsTests : IClassFixture<TestContainersFixture>
{
    private static readonly Faker _faker = new();
    private readonly TestContainersFixture _containers;

    public AuthEndpointsTests(TestContainersFixture containers)
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

    private static async Task<JsonElement> RegisterAsync(HttpClient client, string email, string password = "Password@123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "John",
            last_name = "Doe",
            email,
            password
        });

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json).RootElement.Clone();
    }

    private static async Task<(string AccessToken, string RefreshToken)> LoginAsync(HttpClient client, string email, string password = "Password@123")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!);
    }

    private static async Task<string> GeneratePasswordResetTokenAsync(CustomWebApplicationFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    // AC-AUTH-I01
    [Fact]
    public async Task Should_Return_201_When_Register_With_Valid_Data()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "John",
            last_name = "Doe",
            email = _faker.Internet.Email(),
            password = "Password@123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // AC-AUTH-I02
    [Fact]
    public async Task Should_Return_409_When_Register_With_Duplicate_Email()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "John",
            last_name = "Doe",
            email,
            password = "Password@123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // AC-AUTH-I03
    [Fact]
    public async Task Should_Return_422_When_Register_With_Weak_Password()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            first_name = "John",
            last_name = "Doe",
            email = _faker.Internet.Email(),
            password = "weak"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-AUTH-I04
    [Fact]
    public async Task Should_Return_400_When_Register_With_Empty_Body()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-AUTH-I05
    [Fact]
    public async Task Should_Return_200_With_Tokens_When_Login_With_Valid_Credentials()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("refresh_token").GetString().Should().NotBeNullOrEmpty();
    }

    // AC-AUTH-I06
    [Fact]
    public async Task Should_Return_401_When_Login_With_Wrong_Password()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword@123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I07
    [Fact]
    public async Task Should_Return_423_After_5_Failed_Login_Attempts()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        HttpResponseMessage response = null!;
        for (var i = 0; i < 5; i++)
        {
            response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword@123" });
        }

        response.StatusCode.Should().Be((HttpStatusCode)423);
    }

    // AC-AUTH-I08
    [Fact]
    public async Task Should_Return_400_When_Login_With_Empty_Body()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-AUTH-I09
    [Fact]
    public async Task Should_Return_200_With_New_Tokens_When_Refresh_With_Valid_Token()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var (_, refreshToken) = await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = refreshToken });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("refresh_token").GetString().Should().NotBe(refreshToken);
    }

    // AC-AUTH-I10
    [Fact]
    public async Task Should_Return_401_When_Refresh_With_Invalid_Token()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = "random-nonexistent-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I11
    [Fact]
    public async Task Should_Return_401_When_Refresh_With_Expired_Token()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var (_, refreshToken) = await LoginAsync(client, email);

        using (var scope = factory.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hash = tokenService.HashRefreshToken(refreshToken);

            var tokenRow = await db.UserTokens
                .FirstAsync(t => t.LoginProvider == "EcommerceApi" && t.Name == "RefreshToken" && t.Value!.StartsWith(hash + "|"));

            tokenRow.Value = $"{hash}|{DateTime.UtcNow.AddDays(-1).Ticks}";
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I12
    [Fact]
    public async Task Should_Return_204_When_Logout_With_Valid_Jwt()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var (accessToken, refreshToken) = await LoginAsync(client, email);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // AC-AUTH-I13
    [Fact]
    public async Task Should_Return_401_When_Logout_Without_Jwt()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refresh_token = "some-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I14
    [Fact]
    public async Task Should_Return_429_With_RetryAfter_When_Login_Rate_Limit_Exceeded()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        HttpResponseMessage response = null!;
        for (var i = 0; i < 6; i++)
        {
            response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });
        }

        response.StatusCode.Should().Be((HttpStatusCode)429);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    // AC-AUTH-I15
    [Fact]
    public async Task Should_Return_429_With_RetryAfter_When_Register_Rate_Limit_Exceeded()
    {
        var (_, client) = await CreateClientAsync();

        HttpResponseMessage response = null!;
        for (var i = 0; i < 4; i++)
        {
            response = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                first_name = "John",
                last_name = "Doe",
                email = _faker.Internet.Email(),
                password = "Password@123"
            });
        }

        response.StatusCode.Should().Be((HttpStatusCode)429);
        response.Headers.RetryAfter.Should().NotBeNull();
    }

    // AC-AUTH-I16
    [Fact]
    public async Task Should_Return_200_With_Generic_Message_When_ForgotPassword_With_Existing_Email()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-AUTH-I17
    [Fact]
    public async Task Should_Return_200_With_Same_Generic_Message_When_ForgotPassword_With_NonExisting_Email()
    {
        var (_, client) = await CreateClientAsync();
        var existingEmail = _faker.Internet.Email();
        await RegisterAsync(client, existingEmail);

        var existingResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = existingEmail });
        var nonExistingResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = _faker.Internet.Email() });

        var existingBody = await existingResponse.Content.ReadAsStringAsync();
        var nonExistingBody = await nonExistingResponse.Content.ReadAsStringAsync();

        nonExistingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        nonExistingBody.Should().Be(existingBody);
    }

    // AC-AUTH-I18
    [Fact]
    public async Task Should_Return_400_When_ForgotPassword_With_Empty_Body()
    {
        var (_, client) = await CreateClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // AC-AUTH-I19
    [Fact]
    public async Task Should_Return_200_When_ResetPassword_With_Valid_Token()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var token = await GeneratePasswordResetTokenAsync(factory, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email,
            token,
            new_password = "NewPassword@456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-AUTH-I20
    [Fact]
    public async Task Should_Return_401_When_ResetPassword_With_Expired_Token()
    {
        var (_, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email,
            token = "an-expired-or-malformed-token",
            new_password = "NewPassword@456"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I21
    [Fact]
    public async Task Should_Return_401_When_ResetPassword_With_Already_Used_Token()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var token = await GeneratePasswordResetTokenAsync(factory, email);

        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, new_password = "NewPassword@456" });
        var reusedResponse = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, new_password = "AnotherPassword@789" });

        reusedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I22
    [Fact]
    public async Task Should_Return_422_When_ResetPassword_With_Weak_Password()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var token = await GeneratePasswordResetTokenAsync(factory, email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, new_password = "weak" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // AC-AUTH-I23
    [Fact]
    public async Task Should_Return_401_When_Login_With_Old_Password_After_Reset()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var token = await GeneratePasswordResetTokenAsync(factory, email);

        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, new_password = "NewPassword@456" });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Password@123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AC-AUTH-I24
    [Fact]
    public async Task Should_Return_401_When_Refreshing_With_Token_Issued_Before_Reset()
    {
        var (factory, client) = await CreateClientAsync();
        var email = _faker.Internet.Email();
        await RegisterAsync(client, email);
        var (_, refreshToken) = await LoginAsync(client, email);
        var token = await GeneratePasswordResetTokenAsync(factory, email);

        await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, new_password = "NewPassword@456" });
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = refreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
