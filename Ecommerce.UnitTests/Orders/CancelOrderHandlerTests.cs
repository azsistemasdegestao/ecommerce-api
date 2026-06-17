using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Orders.Commands.CancelOrder;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Orders;

public class CancelOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly CancelOrderHandler _handler;

    public CancelOrderHandlerTests()
    {
        _handler = new CancelOrderHandler(_orderRepositoryMock.Object, _eventBusMock.Object);
    }

    private static Order MakePendingOrder(Guid userId) => Order.Create(
        userId, "123 Main St", [(Guid.NewGuid(), "Product", 1, 10m)]);

    // AC-ORD-U05
    [Fact]
    public async Task Should_Cancel_Pending_Order_And_Publish_OrderCancelled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        var command = new CancelOrderCommand(userId, order.Id);

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(nameof(OrderStatus.Cancelled));
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-ORD-U06
    [Fact]
    public async Task Should_Cancel_Confirmed_Order_And_Publish_OrderCancelled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        order.ChangeStatus(OrderStatus.Confirmed);
        var command = new CancelOrderCommand(userId, order.Id);

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(nameof(OrderStatus.Cancelled));
    }

    // AC-ORD-U07
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Order_Is_Delivered()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Processing);
        order.ChangeStatus(OrderStatus.Shipped);
        order.ChangeStatus(OrderStatus.Delivered);
        var command = new CancelOrderCommand(userId, order.Id);

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-ORD-U08
    [Fact]
    public async Task Should_Throw_ForbiddenException_When_Order_Belongs_To_Another_User()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var order = MakePendingOrder(ownerId);
        var command = new CancelOrderCommand(Guid.NewGuid(), order.Id);

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
