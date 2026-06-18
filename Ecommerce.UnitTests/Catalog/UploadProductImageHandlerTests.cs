using Ecommerce.Application.Catalog.Commands.UploadProductImage;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class UploadProductImageHandlerTests
{
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IImageStorageService> _imageStorageServiceMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly UploadProductImageHandler _handler;

    public UploadProductImageHandlerTests()
    {
        _handler = new UploadProductImageHandler(
            _productRepositoryMock.Object, _imageStorageServiceMock.Object, _eventBusMock.Object);
    }

    // AC-CAT-U10
    [Fact]
    public async Task Should_Upload_Image_Update_Product_And_Publish_ProductUpdated_When_Product_Exists()
    {
        // Arrange
        var product = Product.Create("Name", "Desc", "slug", 10m, 5, "old-url", Guid.NewGuid());
        using var stream = new MemoryStream();
        var command = new UploadProductImageCommand(product.Id, stream, "photo.jpg", "image/jpeg", 1024);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _imageStorageServiceMock
            .Setup(x => x.UploadAsync(stream, "photo.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.local/product-images/new-key.jpg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ImageUrl.Should().Be("https://minio.local/product-images/new-key.jpg");
        product.ImageUrl.Should().Be("https://minio.local/product-images/new-key.jpg");
        _productRepositoryMock.Verify(x => x.Update(product), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<ProductUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-CAT-U11
    [Fact]
    public async Task Should_Throw_NotFoundException_When_Product_Does_Not_Exist()
    {
        // Arrange
        using var stream = new MemoryStream();
        var command = new UploadProductImageCommand(Guid.NewGuid(), stream, "photo.jpg", "image/jpeg", 1024);

        _productRepositoryMock
            .Setup(x => x.GetByIdAsync(command.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _imageStorageServiceMock.Verify(
            x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
