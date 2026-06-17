using Ecommerce.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ecommerce.API.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddApiHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConnection = configuration["DB_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("DB_CONNECTION_STRING is not configured.");

        var redisConnection = configuration["REDIS_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("REDIS_CONNECTION_STRING is not configured.");

        services.AddHealthChecks()
            .AddNpgSql(dbConnection, name: "postgres", tags: ["db", "postgres"])
            .AddRedis(redisConnection, name: "redis", tags: ["cache", "redis"])
            .AddCheck<EventBusHealthCheck>("event_bus", tags: ["eventbus"]);

        return services;
    }

    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });
    }

    private static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration
            })
        });
    }
}
