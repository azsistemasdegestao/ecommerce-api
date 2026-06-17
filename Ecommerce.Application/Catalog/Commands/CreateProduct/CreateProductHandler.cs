using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Common.Helpers;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Commands.CreateProduct;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventBus _eventBus;

    public CreateProductHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IEventBus eventBus)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _eventBus = eventBus;
    }

    public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // BR-CAT-003 / BR-CAT-004
        if (request.Price <= 0)
            throw new UnprocessableEntityException("Price must be greater than zero.");
        if (request.Stock < 0)
            throw new UnprocessableEntityException("Stock cannot be negative.");

        // BR-CAT-006
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            throw new UnprocessableEntityException("Category does not exist.");

        // BR-CAT-002
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Generate(request.Name) : request.Slug;

        // BR-CAT-001
        if (await _productRepository.GetBySlugAsync(slug, cancellationToken) is not null)
            throw new ConflictException("Slug already exists.");

        var product = Product.Create(request.Name, request.Description, slug, request.Price, request.Stock, request.ImageUrl, request.CategoryId);

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ProductCreated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            ProductId: product.Id,
            Slug: product.Slug), cancellationToken);

        return new CreateProductResponse(product.Id, product.Name, product.Slug, product.Price, product.Stock, product.CreatedAt);
    }
}
