using FluentValidation;

namespace Ecommerce.Application.Payments.Commands.RequestPayment;

public sealed class RequestPaymentValidator : AbstractValidator<RequestPaymentCommand>
{
    public RequestPaymentValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
