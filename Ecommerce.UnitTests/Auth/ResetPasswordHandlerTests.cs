using Bogus;
using Ecommerce.Application.Auth.Commands.ResetPassword;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class ResetPasswordHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<IRefreshTokenStore> _refreshTokenStoreMock = new();
    private readonly ResetPasswordHandler _handler;

    public ResetPasswordHandlerTests()
    {
        _handler = new ResetPasswordHandler(_userManagerMock.Object, _refreshTokenStoreMock.Object);
    }

    // AC-AUTH-U15
    [Fact]
    public async Task Should_Reset_Password_And_Remove_Refresh_Tokens_When_Token_Is_Valid()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new ResetPasswordCommand(user.Email, "valid-token", "NewPassword@456");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, command.Token, command.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenStoreMock.Verify(x => x.RemoveAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-AUTH-U16
    [Fact]
    public async Task Should_Throw_AuthenticationFailedException_When_Token_Is_Expired()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new ResetPasswordCommand(user.Email, "expired-token", "NewPassword@456");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, command.Token, command.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    // AC-AUTH-U17 — Identity reports a reused token the same way as an invalid one
    [Fact]
    public async Task Should_Throw_AuthenticationFailedException_When_Token_Was_Already_Used()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new ResetPasswordCommand(user.Email, "already-used-token", "NewPassword@456");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, command.Token, command.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    // AC-AUTH-U18
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_New_Password_Is_Weak()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = _faker.Internet.Email() };
        var command = new ResetPasswordCommand(user.Email, "valid-token", "weak");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, command.Token, command.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Too short." }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }
}
