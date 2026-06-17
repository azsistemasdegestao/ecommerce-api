using MediatR;

namespace Ecommerce.Application.Catalog.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    string? Slug,
    decimal Price,
    int Stock,
    string ImageUrl,
    Guid CategoryId) : IRequest<CreateProductResponse>;
