using Ecommerce.Application.Admin;
using Ecommerce.Application.Admin.Commands.UpdateOrderStatus;
using Ecommerce.Application.Admin.Queries.GetAllOrders;
using Ecommerce.Application.Admin.Queries.GetOrderByIdAdmin;
using Ecommerce.Application.Common.DTOs;
using Ecommerce.Application.Orders;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Endpoints.Admin;

public static class AdminOrdersEndpoints
{
    public static void MapAdminOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/orders")
            .WithTags("Admin - Orders")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapGet("/", GetAllOrders)
            .WithName("AdminGetAllOrders")
            .WithSummary("List all Orders")
            .WithDescription("Lists all system Orders with pagination, restricted to Admins.")
            .Produces<PagedResponse<AdminOrderSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetOrderByIdAdmin)
            .WithName("AdminGetOrderById")
            .WithSummary("Get an Order's details")
            .WithDescription("Returns details of any Order, restricted to Admins.")
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/status", UpdateOrderStatus)
            .WithName("AdminUpdateOrderStatus")
            .WithSummary("Force an Order status update")
            .WithDescription("Forces a status transition on an Order, restricted to Admins.")
            .Produces<UpdateOrderStatusResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> GetAllOrders(
        ISender sender,
        CancellationToken ct,
        int page_number = 1,
        int page_size = 20,
        string? status = null,
        Guid? user_id = null)
    {
        var result = await sender.Send(new GetAllOrdersQuery(page_number, page_size, status, user_id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderByIdAdmin(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetOrderByIdAdminQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateOrderStatus(
        Guid id, UpdateOrderStatusRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateOrderStatusCommand(id, request.Status), ct);
        return Results.Ok(result);
    }
}

public sealed record UpdateOrderStatusRequest(string Status);
