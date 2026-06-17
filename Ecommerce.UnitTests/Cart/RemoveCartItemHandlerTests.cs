using Ecommerce.Application.Cart.Commands.RemoveCartItem;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Cart;

public class RemoveCartItemHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock = new();
    private readonly RemoveCartItemHandler _handler;

    public RemoveCartItemHandlerTests()
    {
        _handler = new RemoveCartItemHandler(_cartRepositoryMock.Object);
    }

    // AC-CART-U08
    [Fact]
    public async Task Should_Remove_Item_When_It_Exists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = Product.Create("Name", "desc", "slug", 10m, 5, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 1, product.Price);
        var item = cart.Items.First();
        var command = new RemoveCartItemCommand(userId, item.Id);

        _cartRepositoryMock.Setup(x => x.GetByItemIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        cart.Items.Should().BeEmpty();
        _cartRepositoryMock.Verify(x => x.Update(cart), Times.Once);
    }

    // AC-CART-U11
    [Fact]
    public async Task Should_Throw_ForbiddenException_When_Item_Belongs_To_Another_User()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var product = Product.Create("Name", "desc", "slug", 10m, 5, "url", Guid.NewGuid());
        var cart = DomainCart.Create(ownerId);
        cart.AddItem(product.Id, 1, product.Price);
        var item = cart.Items.First();
        var command = new RemoveCartItemCommand(Guid.NewGuid(), item.Id);

        _cartRepositoryMock.Setup(x => x.GetByItemIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
