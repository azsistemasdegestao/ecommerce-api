using Ecommerce.Domain.Common;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Ecommerce.Infrastructure.Cache.Handlers;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class ProductCreatedCacheHandlerTests
{
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly ProductCreatedCacheHandler _handler;

    public ProductCreatedCacheHandlerTests()
    {
        _handler = new ProductCreatedCacheHandler(_cacheServiceMock.Object);
    }

    // AC-CAT-U12
    [Fact]
    public async Task Should_Remove_Product_List_Cache_Key_When_Product_Is_Created()
    {
        // Arrange
        var domainEvent = new ProductCreated(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "blue-t-shirt-m");

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _cacheServiceMock.Verify(
            x => x.RemoveAsync(CacheKeys.ProductList("1:20:::::"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
