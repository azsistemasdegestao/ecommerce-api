using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Ecommerce.Infrastructure.Cache.Handlers;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class ProductUpdatedCacheHandlerTests
{
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly ProductUpdatedCacheHandler _handler;

    public ProductUpdatedCacheHandlerTests()
    {
        _handler = new ProductUpdatedCacheHandler(_cacheServiceMock.Object);
    }

    // AC-CAT-U08
    [Fact]
    public async Task Should_Remove_Product_Detail_Cache_Key_When_Product_Is_Updated()
    {
        // Arrange
        var domainEvent = new ProductUpdated(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "blue-t-shirt-m");

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _cacheServiceMock.Verify(
            x => x.RemoveAsync("catalog:product:blue-t-shirt-m", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
