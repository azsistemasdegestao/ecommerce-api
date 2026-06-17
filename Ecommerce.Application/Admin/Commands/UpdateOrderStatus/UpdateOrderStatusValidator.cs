using Ecommerce.Domain.Enums;
using FluentValidation;

namespace Ecommerce.Application.Admin.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<OrderStatus>(s, ignoreCase: true, out _))
            .WithMessage("Invalid status.");
    }
}
