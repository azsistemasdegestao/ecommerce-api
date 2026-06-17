using System.Security.Claims;
using Ecommerce.Application.Common.DTOs;
using Ecommerce.Application.Orders;
using Ecommerce.Application.Orders.Commands.CancelOrder;
using Ecommerce.Application.Orders.Commands.CreateOrder;
using Ecommerce.Application.Orders.Queries.GetOrderById;
using Ecommerce.Application.Orders.Queries.GetOrders;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.API.Endpoints.Orders;

public static class OrdersEndpoints
{
    public static void MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithSummary("Checkout: create an Order from the Cart")
            .WithDescription("Creates an Order from the authenticated Customer's Cart and clears the Cart.")
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("orders");

        group.MapGet("/", GetOrders)
            .WithName("GetOrders")
            .WithSummary("List the authenticated Customer's Orders")
            .WithDescription("Lists the authenticated Customer's Orders with pagination.")
            .Produces<PagedResponse<OrderSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .RequireRateLimiting("user");

        group.MapGet("/{id:guid}", GetOrderById)
            .WithName("GetOrderById")
            .WithSummary("Get an Order's details")
            .WithDescription("Returns details of an Order belonging to the authenticated Customer.")
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .RequireRateLimiting("user");

        group.MapPost("/{id:guid}/cancel", CancelOrder)
            .WithName("CancelOrder")
            .WithSummary("Cancel an Order")
            .WithDescription("Cancels an Order belonging to the authenticated Customer, if its status allows it.")
            .Produces<CancelOrderResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("orders");
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CreateOrderCommand(GetUserId(principal), request.ShippingAddress), ct);
        return Results.Created($"/api/v1/orders/{result.Id}", result);
    }

    private static async Task<IResult> GetOrders(
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken ct,
        int page_number = 1,
        int page_size = 10,
        string? status = null)
    {
        var result = await sender.Send(new GetOrdersQuery(GetUserId(principal), page_number, page_size, status), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderById(Guid id, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderByIdQuery(GetUserId(principal), id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelOrder(Guid id, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new CancelOrderCommand(GetUserId(principal), id), ct);
        return Results.Ok(result);
    }
}

public sealed record CreateOrderRequest(string ShippingAddress);
