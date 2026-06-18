using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ecommerce.Infrastructure.HealthChecks;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public StorageHealthCheck(IAmazonS3 s3Client, IConfiguration configuration)
    {
        _s3Client = s3Client;
        _bucketName = configuration["MINIO_BUCKET_NAME"]
            ?? throw new InvalidOperationException("MINIO_BUCKET_NAME is not configured.");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetBucketLocationAsync(_bucketName, cancellationToken);
            return HealthCheckResult.Healthy("MinIO bucket is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO bucket is unreachable.", ex);
        }
    }
}
