namespace Ecommerce.Application.Auth.Commands.RegisterUser;

public sealed record RegisterUserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTime CreatedAt);
