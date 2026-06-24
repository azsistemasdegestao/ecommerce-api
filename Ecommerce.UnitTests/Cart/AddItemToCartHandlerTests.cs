using Ecommerce.Application.Cart.Commands.AddItemToCart;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Cart;

public class AddItemToCartHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock = new();
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly AddItemToCartHandler _handler;

    public AddItemToCartHandlerTests()
    {
        _handler = new AddItemToCartHandler(_cartRepositoryMock.Object, _productRepositoryMock.Object);
    }

    private static Product MakeProduct(int stock = 10, decimal price = 29.90m) =>
        Product.Create("Blue T-Shirt M", "desc", "blue-t-shirt-m", price, stock, "url", Guid.NewGuid());

    // AC-CART-U01
    [Fact]
    public async Task Should_Create_Cart_And_Item_When_Cart_Does_Not_Exist()
    {
        // Arrange
        var product = MakeProduct();
        var command = new AddItemToCartCommand(Guid.NewGuid(), product.Id, 2);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(command.UserId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainCart?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Quantity.Should().Be(2);
        result.UnitPrice.Should().Be(product.Price);
        _cartRepositoryMock.Verify(x => x.AddAsync(It.IsAny<DomainCart>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-CART-U02
    [Fact]
    public async Task Should_Increment_Quantity_When_Product_Already_In_Cart()
    {
        // Arrange
        var product = MakeProduct(stock: 20);
        var userId = Guid.NewGuid();
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 2, product.Price);
        var command = new AddItemToCartCommand(userId, product.Id, 3);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Quantity.Should().Be(5);
        _cartRepositoryMock.Verify(x => x.Update(It.IsAny<DomainCart>()), Times.Never);
        _cartRepositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-CART-U02
    [Fact]
    public async Task Should_Explicitly_Add_New_Item_When_Product_Not_Yet_In_Existing_Cart()
    {
        // Arrange
        var existingProduct = MakeProduct(stock: 20);
        var newProduct = MakeProduct(stock: 5);
        var userId = Guid.NewGuid();
        var cart = DomainCart.Create(userId);
        cart.AddItem(existingProduct.Id, 1, existingProduct.Price);
        var command = new AddItemToCartCommand(userId, newProduct.Id, 2);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(newProduct.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newProduct);
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Quantity.Should().Be(2);
        _cartRepositoryMock.Verify(x => x.AddItem(It.Is<CartItem>(i => i.ProductId == newProduct.Id)), Times.Once);
        _cartRepositoryMock.Verify(x => x.Update(It.IsAny<DomainCart>()), Times.Never);
    }

    // AC-CART-U03
    [Fact]
    public void Should_Fail_Validation_When_Quantity_Is_Zero()
    {
        // Arrange
        var validator = new AddItemToCartValidator();
        var command = new AddItemToCartCommand(Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    // AC-CART-U04
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Product_Is_Out_Of_Stock()
    {
        // Arrange
        var product = MakeProduct(stock: 0);
        var command = new AddItemToCartCommand(Guid.NewGuid(), product.Id, 1);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(command.UserId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainCart?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CART-U05
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Quantity_Greater_Than_Stock()
    {
        // Arrange
        var product = MakeProduct(stock: 3);
        var command = new AddItemToCartCommand(Guid.NewGuid(), product.Id, 5);

        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(command.UserId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainCart?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CART-I05 (404 path covered at handler level too)
    [Fact]
    public async Task Should_Throw_NotFoundException_When_Product_Does_Not_Exist()
    {
        // Arrange
        var command = new AddItemToCartCommand(Guid.NewGuid(), Guid.NewGuid(), 1);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(command.ProductId, It.IsAny<CancellationToken>())).ReturnsAsync((Product?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
