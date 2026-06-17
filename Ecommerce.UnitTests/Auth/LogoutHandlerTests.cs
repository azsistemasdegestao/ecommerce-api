using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Domain.Interfaces;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class LogoutHandlerTests
{
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _handler = new LogoutHandler(_refreshTokenStoreMock.Object);
    }

    // AC-AUTH-U11
    [Fact]
    public async Task Should_Remove_Refresh_Token_When_Logout_Is_Called()
    {
        // Arrange
        var command = new LogoutCommand(Guid.NewGuid(), "some-refresh-token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenStoreMock.Verify(x => x.RemoveAsync(command.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
