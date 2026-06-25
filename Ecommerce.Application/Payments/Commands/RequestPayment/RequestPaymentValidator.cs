using Ecommerce.Domain.Enums;
using FluentValidation;

namespace Ecommerce.Application.Payments.Commands.RequestPayment;

public sealed class RequestPaymentValidator : AbstractValidator<RequestPaymentCommand>
{
    public RequestPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PaymentMethod)
            .Must(s => Enum.TryParse<PaymentMethod>(s, ignoreCase: true, out _))
            .WithMessage("Invalid payment method.");
    }
}
