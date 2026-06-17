using Bogus;
using Ecommerce.Application.Auth.Commands.ForgotPassword;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class ForgotPasswordHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        _handler = new ForgotPasswordHandler(_userManagerMock.Object, _emailServiceMock.Object);
    }

    // AC-AUTH-U13
    [Fact]
    public async Task Should_Generate_Token_And_Send_Email_When_Email_Exists()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new ForgotPasswordCommand(user.Email);

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            x => x.SendPasswordResetEmailAsync(user.Email, "reset-token", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AC-AUTH-U14
    [Fact]
    public async Task Should_Not_Send_Email_When_Email_Does_Not_Exist()
    {
        // Arrange
        var command = new ForgotPasswordCommand(_faker.Internet.Email());
        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync((ApplicationUser?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(
            x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
