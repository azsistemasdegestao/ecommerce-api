using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce.API.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.Headers.RetryAfter = "60";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    traceId = ctx.HttpContext.TraceIdentifier
                }, ct);
            };

            options.AddFixedWindowLimiter("auth-strict", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 5;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("auth-register", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 3;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("public", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 200;
                o.SegmentsPerWindow = 6;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("user", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 60;
                o.SegmentsPerWindow = 6;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("orders", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 20;
                o.SegmentsPerWindow = 6;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });

            options.AddSlidingWindowLimiter("payment", o =>
            {
                o.Window = TimeSpan.FromMinutes(1);
                o.PermitLimit = 10;
                o.SegmentsPerWindow = 6;
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });
        });

        return services;
    }
}
