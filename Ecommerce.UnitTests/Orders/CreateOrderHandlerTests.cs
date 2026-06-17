using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Orders.Commands.CreateOrder;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Orders;

public class CreateOrderHandlerTests
{
    private readonly Mock<ICartRepository> _cartRepositoryMock = new();
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _handler = new CreateOrderHandler(
            _cartRepositoryMock.Object, _productRepositoryMock.Object, _orderRepositoryMock.Object, _eventBusMock.Object);
    }

    // AC-ORD-U01
    [Fact]
    public async Task Should_Create_Order_And_Clear_Cart_And_Publish_OrderCreated_When_Cart_Is_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = Product.Create("Blue T-Shirt M", "desc", "blue-t-shirt-m", 29.90m, 10, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 2, product.Price);
        var command = new CreateOrderCommand(userId, "123 Main St");

        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(59.80m);
        cart.Items.Should().BeEmpty();
        _orderRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _cartRepositoryMock.Verify(x => x.Update(cart), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-ORD-U02
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Cart_Is_Empty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, "123 Main St");

        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainCart?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-ORD-U03
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Product_Is_Out_Of_Stock()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = Product.Create("Cap", "desc", "cap", 19.90m, 0, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 1, 19.90m);
        var command = new CreateOrderCommand(userId, "123 Main St");

        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-ORD-U04
    [Fact]
    public async Task Should_Snapshot_ProductName_And_UnitPrice_On_OrderItem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = Product.Create("Leather Wallet", "desc", "leather-wallet", 49.90m, 5, "url", Guid.NewGuid());
        var cart = DomainCart.Create(userId);
        cart.AddItem(product.Id, 1, product.Price);
        var command = new CreateOrderCommand(userId, "123 Main St");

        _cartRepositoryMock.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _productRepositoryMock.Setup(x => x.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        Order? capturedOrder = null;
        _orderRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => capturedOrder = o)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var item = capturedOrder!.Items.Single();
        item.ProductName.Should().Be("Leather Wallet");
        item.UnitPrice.Should().Be(49.90m);
    }
}
