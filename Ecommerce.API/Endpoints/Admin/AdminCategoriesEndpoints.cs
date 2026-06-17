using Ecommerce.Application.Admin.Commands.CreateCategory;
using Ecommerce.Application.Admin.Commands.DeleteCategory;
using Ecommerce.Application.Admin.Commands.UpdateCategory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Endpoints.Admin;

public static class AdminCategoriesEndpoints
{
    public static void MapAdminCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/categories")
            .WithTags("Admin - Categories")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapPost("/", CreateCategory)
            .WithName("AdminCreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Creates a new category, restricted to Admins.")
            .Produces<CreateCategoryResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateCategory)
            .WithName("AdminUpdateCategory")
            .WithSummary("Update a category")
            .WithDescription("Updates an existing category, restricted to Admins.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteCategory)
            .WithName("AdminDeleteCategory")
            .WithSummary("Delete a category")
            .WithDescription("Soft-deletes a category without active products, restricted to Admins.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> CreateCategory(CreateCategoryCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Created($"/api/v1/catalog/categories/{result.Slug}", result);
    }

    private static async Task<IResult> UpdateCategory(
        Guid id, UpdateCategoryRequest request, ISender sender, CancellationToken ct)
    {
        await sender.Send(new UpdateCategoryCommand(id, request.Name, request.Slug), ct);
        return Results.Ok(new { message = "Category successfully updated." });
    }

    private static async Task<IResult> DeleteCategory(Guid id, ISender sender, CancellationToken ct)
    {
        await sender.Send(new DeleteCategoryCommand(id), ct);
        return Results.NoContent();
    }
}

public sealed record UpdateCategoryRequest(string Name, string? Slug);
