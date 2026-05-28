// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

public interface IIssueExportService
{
    /// <summary>Build an MS-Project-style xlsx for every issue in the project,
    /// or null if the project doesn't exist. Caller is responsible for
    /// authorisation; the service does not check tenancy beyond what the
    /// scoped DbContext + tenant resolution already enforce.</summary>
    Task<byte[]?> ExportProjectAsync(Guid projectId, CancellationToken ct)
        => ExportProjectAsync(projectId, filter: null, ct);

    /// <summary>Same as the no-filter overload, but restricts the exported rows
    /// to those matching <paramref name="filter"/>. A null or empty filter
    /// exports every issue (identical to the legacy call).</summary>
    Task<byte[]?> ExportProjectAsync(Guid projectId, IssueExportFilter? filter, CancellationToken ct);
}
