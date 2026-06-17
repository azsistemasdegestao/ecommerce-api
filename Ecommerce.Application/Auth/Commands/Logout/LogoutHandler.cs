using Ecommerce.Domain.Interfaces;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenStore _refreshTokenStore;

    public LogoutHandler(IRefreshTokenStore refreshTokenStore)
    {
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _refreshTokenStore.RemoveAsync(request.UserId, cancellationToken);
    }
}
