using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private const int AccessTokenExpiresInSeconds = 3600;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public RefreshTokenHandler(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var providedHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var entry = await _refreshTokenStore.FindByHashAsync(providedHash, cancellationToken);

        if (entry is null || entry.ExpiresAt < DateTime.UtcNow)
            throw new AuthenticationFailedException("Refresh token is invalid or expired.");

        var user = await _userManager.FindByIdAsync(entry.UserId.ToString())
            ?? throw new AuthenticationFailedException("Refresh token is invalid or expired.");

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashRefreshToken(newRefreshToken);

        await _refreshTokenStore.SetAsync(user.Id, newRefreshTokenHash, DateTime.UtcNow.Add(RefreshTokenLifetime), cancellationToken);

        return new LoginResponse(accessToken, newRefreshToken, AccessTokenExpiresInSeconds, "Bearer");
    }
}
