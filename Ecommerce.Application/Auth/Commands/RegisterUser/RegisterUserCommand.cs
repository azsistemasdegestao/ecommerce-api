using MediatR;

namespace Ecommerce.Application.Auth.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<RegisterUserResponse>;
