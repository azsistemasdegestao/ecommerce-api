using Ecommerce.Application.Admin.Commands.DeactivateUser;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.UnitTests.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Admin;

public class DeactivateUserHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly DeactivateUserHandler _handler;

    public DeactivateUserHandlerTests()
    {
        _handler = new DeactivateUserHandler(_userManagerMock.Object);
    }

    // AC-ADMIN-U01
    [Fact]
    public async Task Should_Set_DeletedAt_When_Deactivating_Valid_User()
    {
        // Arrange
        var targetUser = new ApplicationUser { Id = Guid.NewGuid() };
        var command = new DeactivateUserCommand(targetUser.Id, Guid.NewGuid());

        _userManagerMock.Setup(x => x.FindByIdAsync(targetUser.Id.ToString())).ReturnsAsync(targetUser);
        _userManagerMock.Setup(x => x.UpdateAsync(targetUser)).ReturnsAsync(IdentityResult.Success);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        targetUser.DeletedAt.Should().NotBeNull();
        _userManagerMock.Verify(x => x.UpdateAsync(targetUser), Times.Once);
    }

    // AC-ADMIN-U02
    [Fact]
    public async Task Should_Throw_BadRequestException_When_Admin_Deactivates_Themselves()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var command = new DeactivateUserCommand(adminId, adminId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>();
        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }
}
