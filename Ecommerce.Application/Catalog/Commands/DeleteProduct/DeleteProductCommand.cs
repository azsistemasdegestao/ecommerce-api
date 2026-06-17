using MediatR;

namespace Ecommerce.Application.Catalog.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid ProductId) : IRequest;
