using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Commands.UploadProductImage;

public sealed class UploadProductImageHandler : IRequestHandler<UploadProductImageCommand, UploadProductImageResponse>
{
    private readonly IProductRepository _productRepository;
    private readonly IImageStorageService _imageStorageService;
    private readonly IEventBus _eventBus;

    public UploadProductImageHandler(
        IProductRepository productRepository,
        IImageStorageService imageStorageService,
        IEventBus eventBus)
    {
        _productRepository = productRepository;
        _imageStorageService = imageStorageService;
        _eventBus = eventBus;
    }

    public async Task<UploadProductImageResponse> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        var imageUrl = await _imageStorageService.UploadAsync(
            request.FileStream, request.FileName, request.ContentType, cancellationToken);

        product.UpdateImage(imageUrl);

        _productRepository.Update(product);
        await _productRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ProductUpdated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ProductId: product.Id,
            Slug: product.Slug), cancellationToken);

        return new UploadProductImageResponse(product.Id, product.ImageUrl, product.UpdatedAt);
    }
}
