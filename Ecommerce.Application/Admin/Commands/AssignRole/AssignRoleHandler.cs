using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Admin.Commands.AssignRole;

public sealed class AssignRoleHandler : IRequestHandler<AssignRoleCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventBus _eventBus;

    public AssignRoleHandler(UserManager<ApplicationUser> userManager, IEventBus eventBus)
    {
        _userManager = userManager;
        _eventBus = eventBus;
    }

    public async Task Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        // BR-ADMIN-002
        if (request.TargetUserId == request.RequestingAdminId && request.Role != "Admin")
            throw new BadRequestException("Admin cannot remove their own Admin role.");

        var user = await _userManager.FindByIdAsync(request.TargetUserId.ToString())
            ?? throw new NotFoundException("User not found.");

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        await _userManager.AddToRoleAsync(user, request.Role);

        await _eventBus.PublishAsync(new UserRoleAssigned(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserId: user.Id,
            Role: request.Role), cancellationToken);
    }
}
