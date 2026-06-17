using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Auth.Commands.RegisterUser;

public sealed class RegisterUserHandler : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
{
    private const string CustomerRole = "Customer";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEventBus _eventBus;

    public RegisterUserHandler(UserManager<ApplicationUser> userManager, IEventBus eventBus)
    {
        _userManager = userManager;
        _eventBus = eventBus;
    }

    public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new ConflictException("Email already registered.");

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(_ => "password")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            throw new UnprocessableEntityException("Password does not meet requirements.", errors);
        }

        await _userManager.AddToRoleAsync(user, CustomerRole);

        await _eventBus.PublishAsync(new UserRegistered(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserId: user.Id,
            Email: user.Email!), cancellationToken);

        return new RegisterUserResponse(user.Id, user.FirstName, user.LastName, user.Email!, user.CreatedAt);
    }
}
