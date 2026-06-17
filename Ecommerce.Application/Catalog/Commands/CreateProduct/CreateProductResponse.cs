namespace Ecommerce.Application.Catalog.Commands.CreateProduct;

public sealed record CreateProductResponse(
    Guid Id,
    string Name,
    string Slug,
    decimal Price,
    int Stock,
    DateTime CreatedAt);
