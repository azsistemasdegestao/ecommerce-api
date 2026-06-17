using Ecommerce.Application.Admin;
using Ecommerce.Application.Admin.Commands.RefundPayment;
using Ecommerce.Application.Admin.Queries.GetAllPayments;
using Ecommerce.Application.Common.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Endpoints.Admin;

public static class AdminPaymentsEndpoints
{
    public static void MapAdminPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/payments")
            .WithTags("Admin - Payments")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .RequireRateLimiting("user");

        group.MapGet("/", GetAllPayments)
            .WithName("AdminGetAllPayments")
            .WithSummary("List all Payments")
            .WithDescription("Lists all system Payments with pagination, restricted to Admins.")
            .Produces<PagedResponse<AdminPaymentSummaryDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/refund", RefundPayment)
            .WithName("AdminRefundPayment")
            .WithSummary("Refund a Payment")
            .WithDescription("Refunds a Processed Payment and cancels its Order, restricted to Admins.")
            .Produces<RefundPaymentResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> GetAllPayments(
        ISender sender, CancellationToken ct, int page_number = 1, int page_size = 20)
    {
        var result = await sender.Send(new GetAllPaymentsQuery(page_number, page_size), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RefundPayment(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new RefundPaymentCommand(id), ct);
        return Results.Ok(result);
    }
}
