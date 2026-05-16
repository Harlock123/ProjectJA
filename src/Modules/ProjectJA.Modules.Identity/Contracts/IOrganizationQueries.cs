// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Contracts;

public sealed record OrganizationSummary(Guid Id, string Slug, string Name);

public interface IOrganizationQueries
{
    Task<OrganizationSummary?> GetDefaultAsync(CancellationToken ct);
    Task<OrganizationSummary?> GetByIdAsync(Guid organizationId, CancellationToken ct);
}
