using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Admin.Commands.DeactivateUser;

public sealed class DeactivateUserHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DeactivateUserHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        // BR-ADMIN-001
        if (request.TargetUserId == request.RequestingAdminId)
            throw new BadRequestException("Admin cannot deactivate themselves.");

        var user = await _userManager.FindByIdAsync(request.TargetUserId.ToString())
            ?? throw new NotFoundException("User not found.");

        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }
}
