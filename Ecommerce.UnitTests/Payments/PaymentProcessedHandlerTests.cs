using Ecommerce.Application.Payments.EventHandlers;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Payments;

public class PaymentProcessedHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly PaymentProcessedHandler _handler;

    public PaymentProcessedHandlerTests()
    {
        _handler = new PaymentProcessedHandler(
            _paymentRepositoryMock.Object, _orderRepositoryMock.Object, Mock.Of<ILogger<PaymentProcessedHandler>>());
    }

    // AC-PAY-U06
    [Fact]
    public async Task Should_Mark_Payment_Processed_And_Confirm_Order()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "123 Main St", [(Guid.NewGuid(), "Product", 1, 59.80m)]);
        var payment = Payment.Create(order.Id, order.Total, "MockGateway", PaymentMethod.CreditCard);
        payment.StartProcessing();
        var domainEvent = new PaymentProcessed(Guid.NewGuid(), DateTime.UtcNow, payment.Id, order.Id);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Processed);
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Should_Skip_Processing_When_Payment_Is_Not_Processing()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway", PaymentMethod.CreditCard);
        var domainEvent = new PaymentProcessed(Guid.NewGuid(), DateTime.UtcNow, payment.Id, payment.OrderId);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _orderRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
