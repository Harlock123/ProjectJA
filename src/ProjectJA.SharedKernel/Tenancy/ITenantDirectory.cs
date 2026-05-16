// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Tenancy;

public sealed record TenantInfo(TenantId Id, string Slug, string Name, string ConnectionString);

public interface ITenantDirectory
{
    Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken ct);
    Task<TenantInfo?> GetByIdAsync(TenantId id, CancellationToken ct);
    Task<TenantInfo?> GetDefaultAsync(CancellationToken ct);
    Task<IReadOnlyList<TenantInfo>> ListAllAsync(CancellationToken ct);
}
