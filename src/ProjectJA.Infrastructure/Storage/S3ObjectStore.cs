// SPDX-License-Identifier: BUSL-1.1
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ProjectJA.SharedKernel.Storage;

namespace ProjectJA.Infrastructure.Storage;

internal sealed class S3ObjectStore(IAmazonS3 client, IOptions<StorageOptions> options) : IObjectStore
{
    private readonly string _bucket = options.Value.Bucket
        ?? throw new InvalidOperationException("Storage:Bucket is not configured.");

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true, // R2 / MinIO compatibility
        };
        await client.PutObjectAsync(request, ct).ConfigureAwait(false);
    }

    public Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan ttl, CancellationToken ct)
    {
        var expires = DateTime.UtcNow.Add(ttl);
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expires,
        };
        var url = client.GetPreSignedURL(req);
        return Task.FromResult(new PresignedUrl(new Uri(url), new DateTimeOffset(expires)));
    }

    public Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var expires = DateTime.UtcNow.Add(ttl);
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expires,
        };
        var url = client.GetPreSignedURL(req);
        return Task.FromResult(new PresignedUrl(new Uri(url), new DateTimeOffset(expires)));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
        => client.DeleteObjectAsync(_bucket, key, ct);
}
