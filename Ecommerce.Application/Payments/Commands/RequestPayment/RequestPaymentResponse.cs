namespace Ecommerce.Application.Payments.Commands.RequestPayment;

public sealed record RequestPaymentResponse(
    Guid PaymentId, Guid OrderId, decimal Amount, string Status, string PaymentMethod, string Message);
