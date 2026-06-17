using MediatR;

namespace Ecommerce.Application.Payments.Queries.GetPaymentByOrderId;

public sealed record GetPaymentByOrderIdQuery(Guid UserId, Guid OrderId) : IRequest<PaymentDetailDto>;
