using Ecommerce.Domain.Common;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Infrastructure.Cache.Handlers;

public sealed class ProductUpdatedCacheHandler : IEventHandler<ProductUpdated>
{
    private readonly ICacheService _cacheService;

    public ProductUpdatedCacheHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task HandleAsync(ProductUpdated domainEvent, CancellationToken ct = default)
    {
        await _cacheService.RemoveAsync(CacheKeys.ProductDetail(domainEvent.Slug), ct);

        // Listing cache keys are filter-combination specific (no wildcard delete in ICacheService),
        // so only the unfiltered first page is proactively invalidated; other filter combinations
        // age out naturally via their 5-minute TTL.
        await _cacheService.RemoveAsync(CacheKeys.ProductList("1:20:::::"), ct);
    }
}
