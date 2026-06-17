using Ecommerce.Application.Catalog;
using Ecommerce.Application.Catalog.Commands.CreateProduct;
using Ecommerce.Application.Catalog.Commands.DeleteProduct;
using Ecommerce.Application.Catalog.Commands.UpdateProduct;
using Ecommerce.Application.Catalog.Queries.GetCategories;
using Ecommerce.Application.Catalog.Queries.GetProductBySlug;
using Ecommerce.Application.Catalog.Queries.GetProducts;
using Ecommerce.Application.Common.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Endpoints.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/catalog").WithTags("Catalog");

        group.MapGet("/products", GetProducts)
            .WithName("GetProducts")
            .WithSummary("List available products")
            .WithDescription("Lists products with pagination and filters. Public, cached for 5 minutes.")
            .Produces<PagedResponse<ProductSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireRateLimiting("public");

        group.MapGet("/products/{slug}", GetProductBySlug)
            .WithName("GetProductBySlug")
            .WithSummary("Get product details by slug")
            .WithDescription("Returns full product details. Public, cached for 10 minutes.")
            .Produces<ProductDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .RequireRateLimiting("public");

        group.MapGet("/categories", GetCategories)
            .WithName("GetCategories")
            .WithSummary("List all categories")
            .WithDescription("Lists all available categories. Public, cached for 30 minutes.")
            .Produces<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)
            .RequireRateLimiting("public");

        group.MapPost("/products", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a product")
            .WithDescription("Creates a new product, restricted to Admins.")
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapPut("/products/{id:guid}", UpdateProduct)
            .WithName("UpdateProduct")
            .WithSummary("Update a product")
            .WithDescription("Updates an existing product, restricted to Admins.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapDelete("/products/{id:guid}", DeleteProduct)
            .WithName("DeleteProduct")
            .WithSummary("Delete a product")
            .WithDescription("Soft-deletes a product, restricted to Admins.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");
    }

    private static async Task<IResult> GetProducts(
        ISender sender,
        CancellationToken ct,
        int page_number = 1,
        int page_size = 20,
        string? category_slug = null,
        string? search = null,
        decimal? min_price = null,
        decimal? max_price = null,
        bool? in_stock = null)
    {
        var query = new GetProductsQuery(page_number, page_size, category_slug, search, min_price, max_price, in_stock);
        var result = await sender.Send(query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetProductBySlug(string slug, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetProductBySlugQuery(slug), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCategories(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetCategoriesQuery(), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateProduct(CreateProductCommand command, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Results.Created($"/api/v1/catalog/products/{result.Slug}", result);
    }

    private static async Task<IResult> UpdateProduct(
        Guid id, UpdateProductRequest request, ISender sender, CancellationToken ct)
    {
        await sender.Send(new UpdateProductCommand(id, request.Name, request.Description, request.Price, request.Stock, request.ImageUrl), ct);
        return Results.Ok(new { message = "Product successfully updated." });
    }

    private static async Task<IResult> DeleteProduct(Guid id, ISender sender, CancellationToken ct)
    {
        await sender.Send(new DeleteProductCommand(id), ct);
        return Results.NoContent();
    }
}

public sealed record UpdateProductRequest(string Name, string Description, decimal Price, int Stock, string ImageUrl);
