using System.Diagnostics.Metrics;

namespace Ecommerce.Infrastructure.Cache;

public static class CacheMetrics
{
    public const string MeterName = "Ecommerce.Cache";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "cache_requests_total", description: "Cache read attempts, tagged by result (hit/miss).");

    public static void RecordHit() => Requests.Add(1, new KeyValuePair<string, object?>("result", "hit"));

    public static void RecordMiss() => Requests.Add(1, new KeyValuePair<string, object?>("result", "miss"));
}
