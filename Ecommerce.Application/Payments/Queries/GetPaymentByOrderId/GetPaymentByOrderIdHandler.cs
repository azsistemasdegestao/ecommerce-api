using Ecommerce.Application.Common.Exceptions;
using MediatR;

namespace Ecommerce.Application.Payments.Queries.GetPaymentByOrderId;

public sealed class GetPaymentByOrderIdHandler : IRequestHandler<GetPaymentByOrderIdQuery, PaymentDetailDto>
{
    private readonly IPaymentQueryService _paymentQueryService;

    public GetPaymentByOrderIdHandler(IPaymentQueryService paymentQueryService)
    {
        _paymentQueryService = paymentQueryService;
    }

    public async Task<PaymentDetailDto> Handle(GetPaymentByOrderIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await _paymentQueryService.GetByOrderIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException("Payment not found.");

        if (payment.OrderUserId != request.UserId)
            throw new ForbiddenException("This payment does not belong to you.");

        return payment;
    }
}
