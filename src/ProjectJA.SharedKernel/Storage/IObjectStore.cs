// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Storage;

public sealed record PresignedUrl(Uri Url, DateTimeOffset ExpiresAt);

public interface IObjectStore
{
    /// <summary>
    /// Server-side upload. Streams the content into the bucket under the given key.
    /// Used today by the Blazor Server flow; future direct-from-browser uploads should
    /// use <see cref="CreateUploadUrlAsync"/> instead.
    /// </summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);

    Task<PresignedUrl> CreateUploadUrlAsync(string key, string contentType, TimeSpan ttl, CancellationToken ct);
    Task<PresignedUrl> CreateDownloadUrlAsync(string key, TimeSpan ttl, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
