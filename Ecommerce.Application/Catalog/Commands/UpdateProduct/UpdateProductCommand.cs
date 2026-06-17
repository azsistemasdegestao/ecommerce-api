using MediatR;

namespace Ecommerce.Application.Catalog.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string ImageUrl) : IRequest;
