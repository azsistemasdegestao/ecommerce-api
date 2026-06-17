using MediatR;

namespace Ecommerce.Application.Admin.Commands.RefundPayment;

public sealed record RefundPaymentCommand(Guid PaymentId) : IRequest<RefundPaymentResponse>;
