using System.Security.Claims;
using Ecommerce.Application.Admin;
using Ecommerce.Application.Admin.Commands.AssignRole;
using Ecommerce.Application.Admin.Commands.DeactivateUser;
using Ecommerce.Application.Admin.Commands.UnlockUser;
using Ecommerce.Application.Admin.Queries.GetUserById;
using Ecommerce.Application.Admin.Queries.GetUsers;
using Ecommerce.Application.Common.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.API.Endpoints.Admin;

public static class AdminUsersEndpoints
{
    public static void MapAdminUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("Admin - Users")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapGet("/", GetUsers)
            .WithName("AdminGetUsers")
            .WithSummary("List all users")
            .WithDescription("Lists all system users with pagination, restricted to Admins.")
            .Produces<PagedResponse<UserSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetUserById)
            .WithName("AdminGetUserById")
            .WithSummary("Get user details")
            .WithDescription("Returns details of a specific user, restricted to Admins.")
            .Produces<UserDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeactivateUser)
            .WithName("AdminDeactivateUser")
            .WithSummary("Deactivate a user")
            .WithDescription("Soft-deletes a user. An Admin cannot deactivate themselves.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/unlock", UnlockUser)
            .WithName("AdminUnlockUser")
            .WithSummary("Unlock a user")
            .WithDescription("Removes an active lockout from a user.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/roles", AssignRole)
            .WithName("AdminAssignRole")
            .WithSummary("Assign a role to a user")
            .WithDescription("Assigns or changes a user's role (Admin or Customer).")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetUsers(
        ISender sender,
        CancellationToken ct,
        int page_number = 1,
        int page_size = 20,
        string? search = null)
    {
        var result = await sender.Send(new GetUsersQuery(page_number, page_size, search), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUserById(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeactivateUser(
        Guid id, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var adminId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await sender.Send(new DeactivateUserCommand(id, adminId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UnlockUser(Guid id, ISender sender, CancellationToken ct)
    {
        await sender.Send(new UnlockUserCommand(id), ct);
        return Results.Ok(new { message = "User successfully unlocked." });
    }

    private static async Task<IResult> AssignRole(
        Guid id, AssignRoleRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var adminId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await sender.Send(new AssignRoleCommand(id, adminId, request.Role), ct);
        return Results.Ok(new { message = "Role successfully assigned." });
    }
}

public sealed record AssignRoleRequest(string Role);
