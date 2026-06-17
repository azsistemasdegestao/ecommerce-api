using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Commands.DeleteProduct;

public sealed class DeleteProductHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IProductRepository _productRepository;
    private readonly IEventBus _eventBus;

    public DeleteProductHandler(IProductRepository productRepository, IEventBus eventBus)
    {
        _productRepository = productRepository;
        _eventBus = eventBus;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        product.SoftDelete();

        _productRepository.Update(product);
        await _productRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ProductDeleted(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ProductId: product.Id,
            Slug: product.Slug), cancellationToken);
    }
}
