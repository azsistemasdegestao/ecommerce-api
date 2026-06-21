using Ecommerce.Application.Common.Observability;
using Ecommerce.Infrastructure.Cache;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Ecommerce.API.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jaegerEndpoint = configuration["JAEGER_ENDPOINT"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ecommerce-api"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddMeter(CacheMetrics.MeterName)
                .AddPrometheusExporter())
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("Npgsql")
                .AddSource(ApplicationActivitySource.Name)
                .AddRedisInstrumentation()
                .ConfigureRedisInstrumentation((sp, instrumentation) =>
                    instrumentation.AddConnection(sp.GetRequiredService<IConnectionMultiplexer>()))
                .AddAWSInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(jaegerEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                }));

        return services;
    }
}
