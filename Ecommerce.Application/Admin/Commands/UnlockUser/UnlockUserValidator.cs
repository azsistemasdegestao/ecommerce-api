using FluentValidation;

namespace Ecommerce.Application.Admin.Commands.UnlockUser;

public sealed class UnlockUserValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
    }
}
