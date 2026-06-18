namespace Ecommerce.Application.Catalog.Commands.UploadProductImage;

public sealed record UploadProductImageResponse(
    Guid Id,
    string ImageUrl,
    DateTime UpdatedAt);
