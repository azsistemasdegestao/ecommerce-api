using Ecommerce.Application.Cart.Commands.ClearCart;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Cart;

public class ClearCartHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock = new();
    private readonly ClearCartHandler _handler;

    public ClearCartHandlerTests()
    {
        _handler = new ClearCartHandler(_cartRepositoryMock.Object);
    }

    // AC-CART-U09
    [Fact]
    public async Task Should_Remove_All_Items_When_Cart_Has_Items()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = Product.Create("Name", "desc", "slug", 10m, 5, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 2, product.Price);
        var command = new ClearCartCommand(userId);

        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        cart.Items.Should().BeEmpty();
        _cartRepositoryMock.Verify(x => x.Update(cart), Times.Once);
    }

    [Fact]
    public async Task Should_Do_Nothing_When_Cart_Does_Not_Exist()
    {
        // Arrange
        var command = new ClearCartCommand(Guid.NewGuid());
        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(command.UserId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainCart?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _cartRepositoryMock.Verify(x => x.Update(It.IsAny<DomainCart>()), Times.Never);
    }
}
