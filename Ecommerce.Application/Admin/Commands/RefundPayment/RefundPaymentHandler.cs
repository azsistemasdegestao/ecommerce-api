using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Enums;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Admin.Commands.RefundPayment;

public sealed class RefundPaymentHandler : IRequestHandler<RefundPaymentCommand, RefundPaymentResponse>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventBus _eventBus;

    public RefundPaymentHandler(IPaymentRepository paymentRepository, IEventBus eventBus)
    {
        _paymentRepository = paymentRepository;
        _eventBus = eventBus;
    }

    public async Task<RefundPaymentResponse> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        // BR-ADMIN-009
        if (payment.Status != PaymentStatus.Processed)
            throw new UnprocessableEntityException("Payment is not in Processed status.");

        payment.Refund();

        _paymentRepository.Update(payment);
        await _paymentRepository.SaveChangesAsync(cancellationToken);

        // BR-ADMIN-011
        await _eventBus.PublishAsync(new PaymentRefunded(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            PaymentId: payment.Id,
            OrderId: payment.OrderId,
            Amount: payment.Amount), cancellationToken);

        return new RefundPaymentResponse(payment.Id, payment.Status.ToString(), payment.UpdatedAt);
    }
}
