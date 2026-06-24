using Ecommerce.Domain.Common;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Infrastructure.Cache.Handlers;

public sealed class ProductCreatedCacheHandler : IEventHandler<ProductCreated>
{
    private readonly ICacheService _cacheService;

    public ProductCreatedCacheHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task HandleAsync(ProductCreated domainEvent, CancellationToken ct = default)
    {
        // Listing cache keys are filter-combination specific (no wildcard delete in ICacheService),
        // so only the unfiltered first page is proactively invalidated; other filter combinations
        // age out naturally via their 5-minute TTL.
        await _cacheService.RemoveAsync(CacheKeys.ProductList("1:20:::::"), ct);
    }
}
