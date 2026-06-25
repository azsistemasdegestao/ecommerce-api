using Ecommerce.Application.Payments.Commands.RequestPayment;
using FluentValidation.TestHelper;
using Xunit;

namespace Ecommerce.UnitTests.Payments;

public class RequestPaymentValidatorTests
{
    private readonly RequestPaymentValidator _validator = new();

    [Theory]
    [InlineData("CreditCard")]
    [InlineData("Pix")]
    [InlineData("Boleto")]
    [InlineData("pix")]
    public void Should_Not_Have_Error_When_PaymentMethod_Is_Valid(string paymentMethod)
    {
        // Arrange
        var command = new RequestPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), paymentMethod);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void Should_Have_Error_When_PaymentMethod_Is_Not_A_Known_Value()
    {
        // Arrange
        var command = new RequestPaymentCommand(Guid.NewGuid(), Guid.NewGuid(), "Bitcoin");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void Should_Have_Error_When_OrderId_Is_Empty()
    {
        // Arrange
        var command = new RequestPaymentCommand(Guid.NewGuid(), Guid.Empty, "Pix");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.OrderId);
    }
}
