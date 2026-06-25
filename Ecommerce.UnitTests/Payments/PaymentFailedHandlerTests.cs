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

public class PaymentFailedHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly PaymentFailedHandler _handler;

    public PaymentFailedHandlerTests()
    {
        _handler = new PaymentFailedHandler(
            _paymentRepositoryMock.Object, _orderRepositoryMock.Object, Mock.Of<ILogger<PaymentFailedHandler>>());
    }

    // AC-PAY-U07
    [Fact]
    public async Task Should_Mark_Payment_Failed_And_Cancel_Order()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "123 Main St", [(Guid.NewGuid(), "Product", 1, 59.80m)]);
        var payment = Payment.Create(order.Id, order.Total, "MockGateway", PaymentMethod.CreditCard);
        payment.StartProcessing();
        var domainEvent = new PaymentFailed(Guid.NewGuid(), DateTime.UtcNow, payment.Id, order.Id, "Insufficient funds");

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _orderRepositoryMock.Setup(x => x.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        payment.Status.Should().Be(PaymentStatus.Failed);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }
}
