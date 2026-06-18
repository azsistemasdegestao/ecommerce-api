using FluentValidation;

namespace Ecommerce.Application.Catalog.Commands.UploadProductImage;

public sealed class UploadProductImageValidator : AbstractValidator<UploadProductImageCommand>
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public UploadProductImageValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ContentType).Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Only image/jpeg, image/png and image/webp content types are allowed.");
        RuleFor(x => x.FileSize).GreaterThan(0).LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("File size must be between 1 byte and 5MB.");
    }
}
