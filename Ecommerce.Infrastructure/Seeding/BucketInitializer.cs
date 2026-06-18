using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Infrastructure.Seeding;

public static class BucketInitializer
{
    public static async Task EnsureBucketExistsAsync(IServiceProvider services)
    {
        var s3Client = services.GetRequiredService<IAmazonS3>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var bucketName = configuration["MINIO_BUCKET_NAME"]
            ?? throw new InvalidOperationException("MINIO_BUCKET_NAME is not configured.");

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName))
        {
            await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
        }

        var publicReadPolicy = $$"""
            {
              "Version": "2012-10-17",
              "Statement": [
                {
                  "Effect": "Allow",
                  "Principal": "*",
                  "Action": "s3:GetObject",
                  "Resource": "arn:aws:s3:::{{bucketName}}/*"
                }
              ]
            }
            """;

        await s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
        {
            BucketName = bucketName,
            Policy = publicReadPolicy
        });
    }
}
