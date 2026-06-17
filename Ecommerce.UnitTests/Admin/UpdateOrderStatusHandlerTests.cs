using Ecommerce.Application.Admin.Commands.UpdateOrderStatus;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Admin;

public class UpdateOrderStatusHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly UpdateOrderStatusHandler _handler;

    public UpdateOrderStatusHandlerTests()
    {
        _handler = new UpdateOrderStatusHandler(_orderRepositoryMock.Object, _eventBusMock.Object);
    }

    private static Order MakeOrder() => Order.Create(
        Guid.NewGuid(), "123 Main St", [(Guid.NewGuid(), "Product", 1, 10m)]);

    // AC-ADMIN-U07
    [Fact]
    public async Task Should_Update_Status_And_Publish_OrderStatusUpdated_When_Transition_Is_Valid()
    {
        // Arrange
        var order = MakeOrder();
        order.ChangeStatus(OrderStatus.Confirmed);
        var command = new UpdateOrderStatusCommand(order.Id, "Processing");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(nameof(OrderStatus.Processing));
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<OrderStatusUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-ADMIN-U08
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Order_Is_Delivered()
    {
        // Arrange
        var order = MakeOrder();
        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Processing);
        order.ChangeStatus(OrderStatus.Shipped);
        order.ChangeStatus(OrderStatus.Delivered);
        var command = new UpdateOrderStatusCommand(order.Id, "Cancelled");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }
}
