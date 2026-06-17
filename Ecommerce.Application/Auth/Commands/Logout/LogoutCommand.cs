using MediatR;

namespace Ecommerce.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(Guid UserId, string RefreshToken) : IRequest;
