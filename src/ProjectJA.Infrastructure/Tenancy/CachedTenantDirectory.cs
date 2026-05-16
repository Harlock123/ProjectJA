// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.Caching.Memory;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class CachedTenantDirectory(
    TenantDirectory inner,
    IMemoryCache cache,
    TimeSpan ttl) : ITenantDirectory
{
    private const string SlugPrefix = "tenant-directory:slug:";
    private const string IdPrefix = "tenant-directory:id:";
    private const string DefaultKey = "tenant-directory:default";

    public Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken ct)
        => GetOrAddAsync(SlugPrefix + slug, ct => inner.FindBySlugAsync(slug, ct), ct);

    public Task<TenantInfo?> GetByIdAsync(TenantId id, CancellationToken ct)
        => GetOrAddAsync(IdPrefix + id.Value.ToString("N"), ct => inner.GetByIdAsync(id, ct), ct);

    public Task<TenantInfo?> GetDefaultAsync(CancellationToken ct)
        => GetOrAddAsync(DefaultKey, ct => inner.GetDefaultAsync(ct), ct);

    // Bypass cache: callers need fresh listings (provisioning job, retention runner).
    public Task<IReadOnlyList<TenantInfo>> ListAllAsync(CancellationToken ct)
        => inner.ListAllAsync(ct);

    private async Task<TenantInfo?> GetOrAddAsync(
        string key, Func<CancellationToken, Task<TenantInfo?>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out TenantInfo? cached))
            return cached;

        var result = await factory(ct).ConfigureAwait(false);

        // Cache positive hits only. A newly provisioned tenant should be visible
        // immediately on the next request without waiting for a negative entry to expire.
        if (result is not null)
            cache.Set(key, result, ttl);

        return result;
    }
}
