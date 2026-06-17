using FluentValidation;

namespace Ecommerce.Application.Admin.Commands.DeactivateUser;

public sealed class DeactivateUserValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.RequestingAdminId).NotEmpty();
    }
}
