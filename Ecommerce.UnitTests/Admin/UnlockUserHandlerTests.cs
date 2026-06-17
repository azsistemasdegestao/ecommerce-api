using Ecommerce.Application.Admin.Commands.UnlockUser;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.UnitTests.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Admin;

public class UnlockUserHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly UnlockUserHandler _handler;

    public UnlockUserHandlerTests()
    {
        _handler = new UnlockUserHandler(_userManagerMock.Object);
    }

    // AC-ADMIN-U03
    [Fact]
    public async Task Should_Remove_Lockout_When_User_Has_Active_Lockout()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var command = new UnlockUserCommand(user.Id);

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userManagerMock.Verify(x => x.SetLockoutEndDateAsync(user, null), Times.Once);
        _userManagerMock.Verify(x => x.ResetAccessFailedCountAsync(user), Times.Once);
    }

    // AC-ADMIN-U04
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_User_Is_Not_Locked()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var command = new UnlockUserCommand(user.Id);

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
        _userManagerMock.Verify(x => x.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset?>()), Times.Never);
    }
}
