using Ecommerce.Application.Auth.Commands.Login;
using MediatR;

namespace Ecommerce.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;
