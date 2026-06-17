using Ecommerce.Application.Catalog.Commands.UpdateProduct;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class UpdateProductHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly UpdateProductHandler _handler;

    public UpdateProductHandlerTests()
    {
        _handler = new UpdateProductHandler(_productRepositoryMock.Object, _eventBusMock.Object);
    }

    // AC-CAT-U06
    [Fact]
    public async Task Should_Update_Product_And_Publish_ProductUpdated_When_Data_Is_Valid()
    {
        // Arrange
        var product = Product.Create("Old Name", "Old desc", "old-slug", 10m, 5, "url", Guid.NewGuid());
        var command = new UpdateProductCommand(product.Id, "New Name", "New desc", 19.90m, 10, "new-url");

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _productRepositoryMock.Verify(x => x.Update(product), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<ProductUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
