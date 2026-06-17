using Ecommerce.Application.Admin.Commands.AssignRole;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Ecommerce.UnitTests.Auth;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Admin;

public class AssignRoleHandlerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = IdentityMockFactory.CreateUserManagerMock();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly AssignRoleHandler _handler;

    public AssignRoleHandlerTests()
    {
        _handler = new AssignRoleHandler(_userManagerMock.Object, _eventBusMock.Object);
    }

    // AC-ADMIN-U05
    [Fact]
    public async Task Should_Assign_Role_And_Publish_UserRoleAssigned_When_Role_Is_Valid()
    {
        // Arrange
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var command = new AssignRoleCommand(user.Id, Guid.NewGuid(), "Admin");

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Customer" });
        _userManagerMock.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userManagerMock.Verify(x => x.AddToRoleAsync(user, "Admin"), Times.Once);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<UserRoleAssigned>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AC-ADMIN-U06
    [Fact]
    public void Should_Have_Error_When_Role_Is_Invalid()
    {
        // Arrange
        var validator = new AssignRoleValidator();
        var command = new AssignRoleCommand(Guid.NewGuid(), Guid.NewGuid(), "SuperUser");

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
