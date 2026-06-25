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

public class PaymentRequestedHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IMockGatewayService> _gatewayMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly PaymentRequestedHandler _handler;

    public PaymentRequestedHandlerTests()
    {
        _handler = new PaymentRequestedHandler(
            _paymentRepositoryMock.Object, _gatewayMock.Object, _eventBusMock.Object, Mock.Of<ILogger<PaymentRequestedHandler>>());
    }

    private static PaymentRequested MakeEvent(Payment payment) => new(Guid.NewGuid(), DateTime.UtcNow, payment.Id, payment.OrderId, payment.Amount);

    // AC-PAY-U04
    [Fact]
    public async Task Should_Publish_PaymentProcessed_When_Gateway_Approves()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway", PaymentMethod.CreditCard);
        var domainEvent = MakeEvent(payment);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _gatewayMock.Setup(x => x.ProcessAsync(payment.Id, payment.Amount, payment.PaymentMethod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayResult(true, null));

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentProcessed>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentFailed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC-PAY-U05
    [Fact]
    public async Task Should_Publish_PaymentFailed_When_Gateway_Rejects()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway", PaymentMethod.CreditCard);
        var domainEvent = MakeEvent(payment);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        _gatewayMock.Setup(x => x.ProcessAsync(payment.Id, payment.Amount, payment.PaymentMethod, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayResult(false, "Insufficient funds"));

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentFailed>(), It.IsAny<CancellationToken>()), Times.Once);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentProcessed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC-PAY-U08
    [Fact]
    public async Task Should_Skip_Processing_When_Payment_Is_No_Longer_Pending()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway", PaymentMethod.CreditCard);
        payment.StartProcessing();
        payment.MarkProcessed();
        var domainEvent = MakeEvent(payment);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        // Act
        await _handler.HandleAsync(domainEvent, CancellationToken.None);

        // Assert
        _gatewayMock.Verify(x => x.ProcessAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentProcessed>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentFailed>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
