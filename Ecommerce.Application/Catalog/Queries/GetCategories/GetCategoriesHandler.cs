using Ecommerce.Application.Catalog;
using Ecommerce.Domain.Common;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Catalog.Queries.GetCategories;

public sealed class GetCategoriesHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IProductQueryService _productQueryService;
    private readonly ICacheService _cacheService;

    public GetCategoriesHandler(IProductQueryService productQueryService, ICacheService cacheService)
    {
        _productQueryService = productQueryService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cacheService.GetAsync<List<CategoryDto>>(CacheKeys.Categories, cancellationToken);
        if (cached is not null)
            return cached;

        var categories = await _productQueryService.GetCategoriesAsync(cancellationToken);

        await _cacheService.SetAsync(CacheKeys.Categories, categories.ToList(), CacheTtl, cancellationToken);

        return categories;
    }
}
