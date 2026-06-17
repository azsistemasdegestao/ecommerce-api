using Ecommerce.Application.Catalog.Commands.DeleteProduct;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class DeleteProductHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly DeleteProductHandler _handler;

    public DeleteProductHandlerTests()
    {
        _handler = new DeleteProductHandler(_productRepositoryMock.Object, _eventBusMock.Object);
    }

    // AC-CAT-U07
    [Fact]
    public async Task Should_SoftDelete_Product_And_Publish_ProductDeleted_When_Product_Exists()
    {
        // Arrange
        var product = Product.Create("Name", "Desc", "slug", 10m, 5, "url", Guid.NewGuid());
        var command = new DeleteProductCommand(product.Id);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        product.DeletedAt.Should().NotBeNull();
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<Ecommerce.Domain.Events.ProductDeleted>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
