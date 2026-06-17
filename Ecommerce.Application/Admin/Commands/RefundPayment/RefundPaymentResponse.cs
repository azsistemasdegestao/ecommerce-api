namespace Ecommerce.Application.Admin.Commands.RefundPayment;

public sealed record RefundPaymentResponse(Guid Id, string Status, DateTime UpdatedAt);
