using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Auth.Commands.ResetPassword;

public sealed class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public ResetPasswordHandler(UserManager<ApplicationUser> userManager, IRefreshTokenStore refreshTokenStore)
    {
        _userManager = userManager;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new AuthenticationFailedException("Token is invalid or expired.");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
                throw new AuthenticationFailedException("Token is invalid or expired.");

            var errors = result.Errors
                .GroupBy(_ => "new_password")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            throw new UnprocessableEntityException("New password does not meet requirements.", errors);
        }

        await _refreshTokenStore.RemoveAllAsync(user.Id, cancellationToken);
    }
}
