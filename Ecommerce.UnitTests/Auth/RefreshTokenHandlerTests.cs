using Bogus;
using Ecommerce.Application.Auth.Commands.RefreshToken;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class RefreshTokenHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        _handler = new RefreshTokenHandler(_userManagerMock.Object, _tokenServiceMock.Object, _refreshTokenStoreMock.Object);
    }

    // AC-AUTH-U08
    [Fact]
    public async Task Should_Return_New_Tokens_When_Refresh_Token_Is_Valid()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new RefreshTokenCommand("plain-refresh-token");

        _tokenServiceMock.Setup(x => x.HashRefreshToken(command.RefreshToken)).Returns("hashed-token");
        _refreshTokenStoreMock.Setup(x => x.FindByHashAsync("hashed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenEntry(user.Id, DateTime.UtcNow.AddDays(1)));
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Customer" });
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("new-jwt");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("new-refresh-token");
        _tokenServiceMock.Setup(x => x.HashRefreshToken("new-refresh-token")).Returns("new-hashed-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("new-jwt");
        result.RefreshToken.Should().Be("new-refresh-token");
        _refreshTokenStoreMock.Verify(
            x => x.SetAsync(user.Id, "new-hashed-token", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AC-AUTH-U09
    [Fact]
    public async Task Should_Throw_AuthenticationFailedException_When_Refresh_Token_Is_Expired()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired-token");
        _tokenServiceMock.Setup(x => x.HashRefreshToken(command.RefreshToken)).Returns("hashed-expired");
        _refreshTokenStoreMock.Setup(x => x.FindByHashAsync("hashed-expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenEntry(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1)));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    // AC-AUTH-U10
    [Fact]
    public async Task Should_Throw_AuthenticationFailedException_When_Refresh_Token_Was_Already_Used()
    {
        // Arrange — once rotated, the old hash no longer matches any stored entry
        var command = new RefreshTokenCommand("already-used-token");
        _tokenServiceMock.Setup(x => x.HashRefreshToken(command.RefreshToken)).Returns("hashed-used");
        _refreshTokenStoreMock.Setup(x => x.FindByHashAsync("hashed-used", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntry?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }
}
