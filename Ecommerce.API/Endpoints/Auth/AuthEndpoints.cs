using System.Security.Claims;
using Ecommerce.Application.Auth.Commands.ForgotPassword;
using Ecommerce.Application.Auth.Commands.Login;
using Ecommerce.Application.Auth.Commands.Logout;
using Ecommerce.Application.Auth.Commands.RefreshToken;
using Ecommerce.Application.Auth.Commands.RegisterUser;
using Ecommerce.Application.Auth.Commands.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.API.Endpoints.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .WithName("RegisterUser")
            .WithSummary("Register a new customer")
            .WithDescription("Creates a new User with role Customer and publishes UserRegistered.")
            .Produces<RegisterUserResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("auth-register");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate a user")
            .WithDescription("Validates credentials and returns an access token and a refresh token.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status423Locked)
            .RequireRateLimiting("auth-strict");

        group.MapPost("/refresh", Refresh)
            .WithName("RefreshToken")
            .WithSummary("Renew the access token")
            .WithDescription("Issues a new access token and rotates the refresh token.")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("auth-strict");

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Revoke the current refresh token")
            .WithDescription("Invalidates the authenticated user's refresh token.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .RequireAuthorization()
            .RequireRateLimiting("user");

        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Request a password reset")
            .WithDescription("Generates a reset token and simulates sending it by email.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("auth-strict");

        group.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .WithSummary("Reset password using a recovery token")
            .WithDescription("Resets the password and invalidates all refresh tokens.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("auth-strict");
    }

    private static async Task<IResult> Register(RegisterUserCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Created($"/api/v1/auth/{result.Id}", result);
    }

    private static async Task<IResult> Login(LoginCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Refresh(RefreshTokenCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Logout(
        LogoutRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken ct)
    {
        var userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await sender.Send(new LogoutCommand(userId, request.RefreshToken), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPassword(ForgotPasswordCommand command, ISender sender, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return Results.Ok(new { message = "If the email is registered, you will receive instructions shortly." });
    }

    private static async Task<IResult> ResetPassword(ResetPasswordCommand command, ISender sender, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return Results.Ok(new { message = "Password successfully reset." });
    }
}

public sealed record LogoutRequest(string RefreshToken);
