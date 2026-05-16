// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Application;

internal sealed class ProjectQueries(DbContext db) : IProjectQueries
{
    public async Task<ProjectSummary?> GetSummaryAsync(Guid projectId, CancellationToken ct)
    {
        return await db.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectSummary(p.Id, p.OrganizationId, p.Key, p.Name))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int?> AllocateNextIssueNumberAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return null;
        var n = project.AllocateIssueNumber();
        await db.SaveChangesAsync(ct);
        return n;
    }
}
