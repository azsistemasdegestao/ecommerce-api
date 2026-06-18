using Amazon.S3;
using Amazon.S3.Model;
using Ecommerce.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecommerce.Infrastructure.Storage;

public sealed class S3ImageStorageService : IImageStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3ImageStorageService> _logger;
    private readonly string _bucketName;
    private readonly string _publicUrlBase;

    public S3ImageStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3ImageStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["MINIO_BUCKET_NAME"]
            ?? throw new InvalidOperationException("MINIO_BUCKET_NAME is not configured.");
        _publicUrlBase = configuration["MINIO_PUBLIC_URL"]
            ?? throw new InvalidOperationException("MINIO_PUBLIC_URL is not configured.");
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType
        }, ct);

        return $"{_publicUrlBase}/{_bucketName}/{key}";
    }

    public async Task DeleteAsync(string imageUrl, CancellationToken ct = default)
    {
        var prefix = $"{_publicUrlBase}/{_bucketName}/";
        if (!imageUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            _logger.LogWarning("Skipping delete for non-managed image URL {ImageUrl}", imageUrl);
            return;
        }

        var key = imageUrl[prefix.Length..];
        await _s3Client.DeleteObjectAsync(_bucketName, key, ct);
    }
}
