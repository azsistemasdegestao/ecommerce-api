using Bogus;
using Ecommerce.Application.Catalog.Commands.CreateProduct;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Catalog;

public class CreateProductHandlerTests
{
    private static readonly Faker _faker = new();

    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<ICategoryRepository> _categoryRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly CreateProductHandler _handler;

    public CreateProductHandlerTests()
    {
        _handler = new CreateProductHandler(_productRepositoryMock.Object, _categoryRepositoryMock.Object, _eventBusMock.Object);
    }

    private static CreateProductCommand ValidCommand(Guid categoryId) => new(
        _faker.Commerce.ProductName(), _faker.Commerce.ProductDescription(), null, 29.90m, 50, "https://example.com/img.png", categoryId);

    // AC-CAT-U01
    [Fact]
    public async Task Should_Create_Product_And_Publish_ProductCreated_When_Data_Is_Valid()
    {
        // Arrange
        var category = Category.Create("T-Shirts", "t-shirts");
        var command = ValidCommand(category.Id);

        _categoryRepositoryMock.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _productRepositoryMock.Setup(x => x.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be(command.Name);
        _productRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<ProductCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-CAT-U02
    [Fact]
    public async Task Should_Throw_ConflictException_When_Slug_Already_Exists()
    {
        // Arrange
        var category = Category.Create("T-Shirts", "t-shirts");
        var command = ValidCommand(category.Id) with { Slug = "existing-slug" };
        var existingProduct = Product.Create("Other", "desc", "existing-slug", 10m, 1, "url", category.Id);

        _categoryRepositoryMock.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _productRepositoryMock.Setup(x => x.GetBySlugAsync("existing-slug", It.IsAny<CancellationToken>())).ReturnsAsync(existingProduct);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    // AC-CAT-U03
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Price_Is_Zero()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid()) with { Price = 0 };

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CAT-U04
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Stock_Is_Negative()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid()) with { Stock = -1 };

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CAT-U05
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Category_Does_Not_Exist()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid());
        _categoryRepositoryMock.Setup(x => x.GetByIdAsync(command.CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync((Category?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CAT-U09
    [Fact]
    public async Task Should_Generate_Slug_When_Absent()
    {
        // Arrange
        var category = Category.Create("T-Shirts", "t-shirts");
        var command = new CreateProductCommand("Blue T-Shirt M", "desc", null, 29.90m, 50, "url", category.Id);

        _categoryRepositoryMock.Setup(x => x.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _productRepositoryMock.Setup(x => x.GetBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Slug.Should().Be("blue-t-shirt-m");
    }
}
