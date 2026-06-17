using Bogus;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class LoginHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _signInManagerMock = IdentityMockFactory.CreateSignInManagerMock(_userManagerMock.Object);
        _handler = new LoginHandler(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenServiceMock.Object,
            _refreshTokenStoreMock.Object,
            _eventBusMock.Object);
    }

    // AC-AUTH-U05
    [Fact]
    public async Task Should_Return_Tokens_And_Publish_UserLoggedIn_When_Credentials_Are_Valid()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new LoginCommand(user.Email, "Password@123");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, command.Password, true))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Customer" });
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("fake-jwt-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("fake-refresh-token");
        _tokenServiceMock.Setup(x => x.HashRefreshToken("fake-refresh-token")).Returns("hashed-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().Be(3600);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<UserLoggedIn>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AC-AUTH-U06
    [Fact]
    public async Task Should_Throw_AuthenticationFailedException_When_Password_Is_Wrong()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new LoginCommand(user.Email, "WrongPassword@123");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, command.Password, true))
            .ReturnsAsync(SignInResult.Failed);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    // AC-AUTH-U07
    [Fact]
    public async Task Should_Throw_AccountLockedException_When_Account_Is_Locked_Out()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new LoginCommand(user.Email, "Password@123");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, command.Password, true))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountLockedException>();
    }

    // AC-AUTH-U12
    [Fact]
    public async Task Should_Throw_Same_Exception_Type_With_Same_Message_When_Email_Does_Not_Exist()
    {
        // Arrange
        var command = new LoginCommand(_faker.Internet.Email(), "Password@123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<AuthenticationFailedException>();
        exception.Which.Message.Should().Be("Invalid email or password.");
    }
}
