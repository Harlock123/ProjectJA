// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.SharedKernel.Storage;

namespace ProjectJA.Infrastructure.Storage;

/// <summary>
/// Fallback registered when storage isn't configured. Every method throws a clear
/// "Storage not configured" exception so misconfiguration surfaces loudly instead
/// of producing mysterious 500s deep in upload code paths.
/// </summary>
internal sealed class NullObjectStore : IObjectStore
{
    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        => Task.FromException(NotConfigured());
    public Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan ttl, CancellationToken ct)
        => Task.FromException<PresignedUrl>(NotConfigured());
    public Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan ttl, CancellationToken ct)
        => Task.FromException<PresignedUrl>(NotConfigured());
    public Task DeleteAsync(string key, CancellationToken ct)
        => Task.FromException(NotConfigured());

    private static InvalidOperationException NotConfigured() => new(
        "Object storage is not configured. Set Storage:Endpoint, Storage:Bucket, Storage:AccessKey, Storage:SecretKey.");
}
