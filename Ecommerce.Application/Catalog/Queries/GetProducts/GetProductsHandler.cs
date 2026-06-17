using Ecommerce.Application.Catalog;
using Ecommerce.Application.Common.DTOs;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetProducts;

public sealed class GetProductsHandler : IRequestHandler<GetProductsQuery, PagedResponse<ProductSummaryDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IProductQueryService _productQueryService;
    private readonly ICacheService _cacheService;

    public GetProductsHandler(IProductQueryService productQueryService, ICacheService cacheService)
    {
        _productQueryService = productQueryService;
        _cacheService = cacheService;
    }

    public async Task<PagedResponse<ProductSummaryDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var filters = $"{request.PageNumber}:{request.PageSize}:{request.CategorySlug}:{request.Search}:{request.MinPrice}:{request.MaxPrice}:{request.InStock}";
        var cacheKey = CacheKeys.ProductList(filters);


        var cached = await _cacheService.GetAsync<PagedResponse<ProductSummaryDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var result = await _productQueryService.GetProductsAsync(
            request.PageNumber, request.PageSize, request.CategorySlug, request.Search,
            request.MinPrice, request.MaxPrice, request.InStock, cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, CacheTtl, cancellationToken);

        return result;
    }
}
