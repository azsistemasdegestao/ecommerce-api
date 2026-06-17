using Bogus;
using Ecommerce.Application.Auth.Commands.RegisterUser;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class RegisterUserHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(_userManagerMock.Object, _eventBusMock.Object);
    }

    // AC-AUTH-U01
    [Fact]
    public async Task Should_Create_User_And_Publish_UserRegistered_When_Data_Is_Valid()
    {
        // Arrange
        var command = new RegisterUserCommand(
            _faker.Name.FirstName(), _faker.Name.LastName(), _faker.Internet.Email(), "Password@123");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), command.Password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Email.Should().Be(command.Email);
        _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Customer"), Times.Once);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<UserRegistered>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AC-AUTH-U02
    [Fact]
    public async Task Should_Throw_ConflictException_When_Email_Already_Exists()
    {
        // Arrange
        var existingUser = new ApplicationUser { Email = _faker.Internet.Email() };
        var command = new RegisterUserCommand(
            _faker.Name.FirstName(), _faker.Name.LastName(), existingUser.Email, "Password@123");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync(existingUser);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    // AC-AUTH-U03
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Password_Does_Not_Meet_Requirements()
    {
        // Arrange
        var command = new RegisterUserCommand(
            _faker.Name.FirstName(), _faker.Name.LastName(), _faker.Internet.Email(), "weak");

        _userManagerMock.Setup(x => x.FindByEmailAsync(command.Email)).ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), command.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "Password too short." }));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }
}
