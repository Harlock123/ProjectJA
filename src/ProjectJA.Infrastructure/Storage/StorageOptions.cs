// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Infrastructure.Storage;

public sealed class StorageOptions
{
    /// <summary>S3-compatible endpoint URL (e.g. R2 endpoint, MinIO URL, blank for AWS S3).</summary>
    public string? Endpoint { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Region { get; set; } = "us-east-1";

    /// <summary>True when running against MinIO or non-AWS endpoints that require path-style addressing.</summary>
    public bool ForcePathStyle { get; set; } = true;
}
