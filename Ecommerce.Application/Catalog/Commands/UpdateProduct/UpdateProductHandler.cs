using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Commands.UpdateProduct;

public sealed class UpdateProductHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IEventBus _eventBus;

    public UpdateProductHandler(IProductRepository productRepository, IEventBus eventBus)
    {
        _productRepository = productRepository;
        _eventBus = eventBus;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.Price <= 0)
            throw new UnprocessableEntityException("Price must be greater than zero.");
        if (request.Stock < 0)
            throw new UnprocessableEntityException("Stock cannot be negative.");

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        product.Update(request.Name, request.Description, request.Price, request.Stock, request.ImageUrl);

        _productRepository.Update(product);
        await _productRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ProductUpdated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ProductId: product.Id,
            Slug: product.Slug), cancellationToken);
    }
}
