using Ecommerce.Application.Catalog.Commands.UploadProductImage;
using FluentValidation.TestHelper;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class UploadProductImageValidatorTests
{
    private readonly UploadProductImageValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Data_Is_Valid()
    {
        // Arrange
        var command = new UploadProductImageCommand(Guid.NewGuid(), Stream.Null, "photo.jpg", "image/jpeg", 1024);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_ContentType_Is_Not_Allowed()
    {
        // Arrange
        var command = new UploadProductImageCommand(Guid.NewGuid(), Stream.Null, "photo.gif", "image/gif", 1024);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Fact]
    public void Should_Have_Error_When_FileSize_Exceeds_5MB()
    {
        // Arrange
        var command = new UploadProductImageCommand(Guid.NewGuid(), Stream.Null, "photo.jpg", "image/jpeg", 6 * 1024 * 1024);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileSize);
    }

    [Fact]
    public void Should_Have_Error_When_FileSize_Is_Zero()
    {
        // Arrange
        var command = new UploadProductImageCommand(Guid.NewGuid(), Stream.Null, "photo.jpg", "image/jpeg", 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileSize);
    }
}
