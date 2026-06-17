using Ecommerce.Application.Admin.Commands.RefundPayment;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Ecommerce.UnitTests.Admin;

public class RefundPaymentHandlerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly RefundPaymentHandler _handler;

    public RefundPaymentHandlerTests()
    {
        _handler = new RefundPaymentHandler(_paymentRepositoryMock.Object, _eventBusMock.Object);
    }

    // AC-ADMIN-U09
    [Fact]
    public async Task Should_Refund_Processed_Payment_And_Publish_PaymentRefunded()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway");
        payment.StartProcessing();
        payment.MarkProcessed();
        var command = new RefundPaymentCommand(payment.Id);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(nameof(PaymentStatus.Refunded));
        _eventBusMock.Verify(x => x.PublishAsync(It.IsAny<PaymentRefunded>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC-ADMIN-U10
    [Fact]
    public async Task Should_Throw_UnprocessableEntityException_When_Payment_Is_Pending()
    {
        // Arrange
        var payment = Payment.Create(Guid.NewGuid(), 59.80m, "MockGateway");
        var command = new RefundPaymentCommand(payment.Id);

        _paymentRepositoryMock.Setup(x => x.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnprocessableEntityException>();
    }
}
