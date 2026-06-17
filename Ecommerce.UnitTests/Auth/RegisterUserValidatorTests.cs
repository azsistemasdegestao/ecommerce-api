using Ecommerce.Application.Auth.Commands.RegisterUser;
using FluentValidation.TestHelper;
using Xunit;

namespace Ecommerce.UnitTests.Auth;

public class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _validator = new();

    // AC-AUTH-U04
    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        // Arrange
        var command = new RegisterUserCommand("John", "Doe", string.Empty, "Password@123");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
