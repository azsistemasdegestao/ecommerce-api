using Ecommerce.Application.Catalog;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetProductBySlug;

public sealed class GetProductBySlugHandler : IRequestHandler<GetProductBySlugQuery, ProductDetailDto>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IProductQueryService _productQueryService;
    private readonly ICacheService _cacheService;

    public GetProductBySlugHandler(IProductQueryService productQueryService, ICacheService cacheService)
    {
        _productQueryService = productQueryService;
        _cacheService = cacheService;
    }

    public async Task<ProductDetailDto> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.ProductDetail(request.Slug);

        var cached = await _cacheService.GetAsync<ProductDetailDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var product = await _productQueryService.GetBySlugAsync(request.Slug, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        await _cacheService.SetAsync(cacheKey, product, CacheTtl, cancellationToken);

        return product;
    }
}
