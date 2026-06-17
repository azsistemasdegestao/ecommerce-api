using Ecommerce.Domain.Common;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Infrastructure.Cache.Handlers;

public sealed class ProductDeletedCacheHandler : IEventHandler<ProductDeleted>
{
    private readonly ICacheService _cacheService;

    public ProductDeletedCacheHandler(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task HandleAsync(ProductDeleted domainEvent, CancellationToken ct = default)
    {
        await _cacheService.RemoveAsync(CacheKeys.ProductDetail(domainEvent.Slug), ct);
        await _cacheService.RemoveAsync(CacheKeys.ProductList("1:20:::::"), ct);
    }
}
