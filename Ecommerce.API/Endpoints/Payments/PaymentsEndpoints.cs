using System.Security.Claims;
using Ecommerce.Application.Payments;
using Ecommerce.Application.Payments.Commands.RequestPayment;
using Ecommerce.Application.Payments.Queries.GetPaymentByOrderId;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Ecommerce.API.Endpoints.Payments;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        group.MapPost("/", RequestPayment)
            .WithName("RequestPayment")
            .WithSummary("Initiate payment for an Order")
            .WithDescription("Starts asynchronous payment processing for an Order belonging to the authenticated Customer.")
            .Produces<RequestPaymentResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
            .RequireRateLimiting("payment");

        group.MapGet("/{orderId:guid}", GetPaymentByOrderId)
            .WithName("GetPaymentByOrderId")
            .WithSummary("Check payment status for an Order")
            .WithDescription("Returns the Payment status for an Order belonging to the authenticated Customer.")
            .Produces<PaymentDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .RequireRateLimiting("user");
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static async Task<IResult> RequestPayment(
        RequestPaymentRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(
            new RequestPaymentCommand(GetUserId(principal), request.OrderId, request.PaymentMethod), ct);
        return Results.Accepted($"/api/v1/payments/{result.OrderId}", result);
    }

    private static async Task<IResult> GetPaymentByOrderId(Guid orderId, ClaimsPrincipal principal, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetPaymentByOrderIdQuery(GetUserId(principal), orderId), ct);
        return Results.Ok(result);
    }
}

public sealed record RequestPaymentRequest(Guid OrderId, string PaymentMethod);
