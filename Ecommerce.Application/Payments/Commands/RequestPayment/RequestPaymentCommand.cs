using MediatR;

namespace Ecommerce.Application.Payments.Commands.RequestPayment;

public sealed record RequestPaymentCommand(Guid UserId, Guid OrderId) : IRequest<RequestPaymentResponse>;
