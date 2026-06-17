using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Admin.Commands.UnlockUser;

public sealed class UnlockUserHandler : IRequestHandler<UnlockUserCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UnlockUserHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.TargetUserId.ToString())
            ?? throw new NotFoundException("User not found.");

        // BR-ADMIN-005
        if (!await _userManager.IsLockedOutAsync(user))
            throw new UnprocessableEntityException("User is not locked.");

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
    }
}
