using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Application.Payments.Commands.RequestPayment;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Payments;

public class RequestPaymentHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly RequestPaymentHandler _handler;

    public RequestPaymentHandlerTests()
    {
        _handler = new RequestPaymentHandler(_orderRepositoryMock.Object, _paymentRepositoryMock.Object, _eventBusMock.Object);
    }

    private static Order MakePendingOrder(Guid userId) => Order.Create(
        userId, "123 Main St", [(Guid.NewGuid(), "Product", 1, 59.80m)]);

    // AC-PAY-U01
    [Fact]
    public async Task Should_Create_Payment_And_Publish_PaymentRequested_When_Order_Is_Valid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        var command = new RequestPaymentCommand(userId, order.Id, "CreditCard");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentRepositoryMock.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Amount.Should().Be(order.Total);
        result.Status.Should().Be(nameof(PaymentStatus.Pending));
        result.PaymentMethod.Should().Be(nameof(PaymentMethod.CreditCard));
        _paymentRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentRequested>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-PAY-U02
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Order_Is_Not_Pending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        order.ChangeStatus(OrderStatus.Confirmed);
        var command = new RequestPaymentCommand(userId, order.Id, "CreditCard");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    // AC-PAY-U03
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Order_Belongs_To_Another_Customer()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var order = MakePendingOrder(ownerId);
        var command = new RequestPaymentCommand(Guid.NewGuid(), order.Id, "CreditCard");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Payment_Already_Requested()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        var existingPayment = Payment.Create(order.Id, order.Total, "MockGateway", PaymentMethod.CreditCard);
        var command = new RequestPaymentCommand(userId, order.Id, "CreditCard");

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentRepositoryMock.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingPayment);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }

    [Fact]
    public async Task Should_Persist_The_Chosen_PaymentMethod_Case_Insensitively()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var order = MakePendingOrder(userId);
        var command = new RequestPaymentCommand(userId, order.Id, "pix");
        Payment? createdPayment = null;

        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentRepositoryMock.Setup(x => x.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Payment?)null);
        _paymentRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => createdPayment = payment);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PaymentMethod.Should().Be(nameof(PaymentMethod.Pix));
        createdPayment!.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }
}
