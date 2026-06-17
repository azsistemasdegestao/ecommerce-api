using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Auth.Commands.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private const int AccessTokenExpiresInSeconds = 3600;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IEventBus _eventBus;

    public LoginHandler(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        IEventBus eventBus)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
        _eventBus = eventBus;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new AuthenticationFailedException("Invalid email or password.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            throw new AccountLockedException("Account is locked due to multiple failed login attempts.");

        if (!result.Succeeded)
            throw new AuthenticationFailedException("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        await _refreshTokenStore.SetAsync(user.Id, refreshTokenHash, DateTime.UtcNow.Add(RefreshTokenLifetime), cancellationToken);

        await _eventBus.PublishAsync(new UserLoggedIn(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            UserId: user.Id,
            Email: user.Email!), cancellationToken);

        return new LoginResponse(accessToken, refreshToken, AccessTokenExpiresInSeconds, "Bearer");
    }
}
