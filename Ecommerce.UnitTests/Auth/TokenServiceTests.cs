using Ecommerce.Domain.Entities;
using Ecommerce.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "unit-test-secret-key-minimum-32-characters!!",
                ["JWT_ISSUER"] = "ecommerce-api",
                ["JWT_AUDIENCE"] = "ecommerce-client"
            })
            .Build();

        _tokenService = new TokenService(configuration);
    }

    [Fact]
    public void Should_Generate_Jwt_With_Three_Segments_When_Generating_Access_Token()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        var roles = new List<string> { "Customer" };

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void Should_Generate_Unique_Tokens_When_Generating_Refresh_Token_Multiple_Times()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Should_Produce_Same_Hash_When_Hashing_Same_Token_Twice()
    {
        // Arrange
        const string token = "sample-refresh-token";

        // Act
        var hash1 = _tokenService.HashRefreshToken(token);
        var hash2 = _tokenService.HashRefreshToken(token);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Should_Produce_Different_Hash_When_Hashing_Different_Tokens()
    {
        // Act
        var hash1 = _tokenService.HashRefreshToken("token-a");
        var hash2 = _tokenService.HashRefreshToken("token-b");

        // Assert
        hash1.Should().NotBe(hash2);
    }
}
