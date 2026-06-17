using FluentValidation;

namespace Ecommerce.Application.Admin.Commands.AssignRole;

public sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    private static readonly string[] ValidRoles = ["Admin", "Customer"];

    public AssignRoleValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => ValidRoles.Contains(role))
            .WithMessage("Role must be either 'Admin' or 'Customer'.");
    }
}
