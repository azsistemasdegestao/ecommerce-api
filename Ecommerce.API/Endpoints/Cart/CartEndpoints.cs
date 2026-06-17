using System.Security.Claims;
using Ecommerce.Application.Cart;
using Ecommerce.Application.Cart.Commands.AddItemToCart;
using Ecommerce.Application.Cart.Commands.ClearCart;
using Ecommerce.Application.Cart.Commands.RemoveCartItem;
using Ecommerce.Application.Cart.Commands.UpdateCartItem;
using Ecommerce.Application.Cart.Queries.GetCart;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.API.Endpoints.Cart;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cart")
            .WithTags("Cart")
            .RequireAuthorization()
            .RequireRateLimiting("user");

        group.MapGet("/", GetCart)
            .WithName("GetCart")
            .WithSummary("Get the authenticated Customer's Cart")
            .WithDescription("Returns the active Cart of the authenticated Customer, or an empty Cart if none exists.")
            .Produces<CartDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/items", AddItemToCart)
            .WithName("AddItemToCart")
            .WithSummary("Add a product to the Cart")
            .WithDescription("Adds a product to the Cart, or increments its quantity if already present.")
            .Produces<AddItemToCartResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/items/{itemId:guid}", UpdateCartItem)
            .WithName("UpdateCartItem")
            .WithSummary("Update the quantity of a CartItem")
            .WithDescription("Updates the quantity of a CartItem belonging to the authenticated Customer.")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/items/{itemId:guid}", RemoveCartItem)
            .WithName("RemoveCartItem")
            .WithSummary("Remove a CartItem")
            .WithDescription("Removes a CartItem belonging to the authenticated Customer.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/", ClearCart)
            .WithName("ClearCart")
            .WithSummary("Clear the Cart")
            .WithDescription("Removes all items from the authenticated Customer's Cart.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> GetCart(ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetCartQuery(GetUserId(principal)), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddItemToCart(
        AddItemToCartRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new AddItemToCartCommand(GetUserId(principal), request.ProductId, request.Quantity), ct);
        return Results.Created($"/api/v1/cart/items/{result.ItemId}", result);
    }

    private static async Task<IResult> UpdateCartItem(
        Guid itemId, UpdateCartItemRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        await sender.Send(new UpdateCartItemCommand(GetUserId(principal), itemId, request.Quantity), ct);
        return Results.Ok(new { message = "Cart item successfully updated." });
    }

    private static async Task<IResult> RemoveCartItem(Guid itemId, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        await sender.Send(new RemoveCartItemCommand(GetUserId(principal), itemId), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearCart(ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        await sender.Send(new ClearCartCommand(GetUserId(principal)), ct);
        return Results.NoContent();
    }
}

public sealed record AddItemToCartRequest(Guid ProductId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);
