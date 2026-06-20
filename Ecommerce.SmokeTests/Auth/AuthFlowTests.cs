using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ecommerce.SmokeTests.Infrastructure;
using FluentAssertions;

namespace Ecommerce.SmokeTests.Auth;

[Collection("Smoke API")]
public sealed class AuthFlowTests
{
    private readonly SmokeApiFixture _fixture;

    public AuthFlowTests(SmokeApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Should_Return409_When_RegisteringWithDuplicateEmail()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/register", new
        {
            first_name = "Smoke",
            last_name = "Tester",
            email = _fixture.CustomerEmail,
            password = _fixture.CustomerPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Should_Return401_When_LoggingInWithWrongPassword()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/login", new
        {
            email = _fixture.CustomerEmail,
            password = "WrongPassword@999"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return200_When_RefreshingToken()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Post, "/api/v1/auth/refresh", new
        {
            refresh_token = _fixture.CustomerRefreshToken
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Return401_When_AccessingCartWithoutToken()
    {
        var response = await _fixture.Client.SendJsonAsync(HttpMethod.Get, "/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
