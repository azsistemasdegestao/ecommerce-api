using Ecommerce.Application.Cart.Commands.UpdateCartItem;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Cart;

public class UpdateCartItemHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock = new();
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly UpdateCartItemHandler _handler;

    public UpdateCartItemHandlerTests()
    {
        _handler = new UpdateCartItemHandler(_cartRepositoryMock.Object, _productRepositoryMock.Object);
    }

    private static (DomainCart cart, CartItem item, Product product) MakeCartWithItem(Guid userId, int stock = 10)
    {
        var product = Product.Create("Blue T-Shirt M", "desc", "blue-t-shirt-m", 29.90m, stock, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 1, product.Price);
        var item = cart.Items.First();
        return (cart, item, product);
    }

    // AC-CART-U06
    [Fact]
    public async Task Should_Update_Quantity_When_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (cart, item, product) = MakeCartWithItem(userId, stock: 10);
        var command = new UpdateCartItemCommand(userId, item.Id, 5);

        _cartRepositoryMock.Setup(x => x.GetByItemIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        item.Quantity.Should().Be(5);
        _cartRepositoryMock.Verify(x => x.Update(cart), Times.Once);
    }

    // AC-CART-U07
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Quantity_Greater_Than_Stock()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (cart, item, product) = MakeCartWithItem(userId, stock: 3);
        var command = new UpdateCartItemCommand(userId, item.Id, 10);

        _cartRepositoryMock.Setup(x => x.GetByItemIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-CART-U11
    [Fact]
    public async Task Should_Throw_ForbiddenException_When_Item_Belongs_To_Another_User()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var (cart, item, _) = MakeCartWithItem(ownerId);
        var command = new UpdateCartItemCommand(Guid.NewGuid(), item.Id, 2);

        _cartRepositoryMock.Setup(x => x.GetByItemIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
