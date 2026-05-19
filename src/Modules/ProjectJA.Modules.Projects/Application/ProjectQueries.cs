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

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken ct) =>
        db.Set<Project>().AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.Members.Any(m => m.UserId == userId), ct);

    public async Task<ProjectRole?> GetRoleAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var roles = await db.Set<Project>().AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Members)
            .Where(m => m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role)
            .FirstOrDefaultAsync(ct);
        return roles;
    }

    public async Task<IReadOnlyList<ProjectMemberInfo>> ListMembersAsync(Guid projectId, CancellationToken ct)
    {
        return await db.Set<Project>().AsNoTracking()
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Members)
            .OrderBy(m => m.AddedAt)
            .Select(m => new ProjectMemberInfo(m.UserId, m.Role, m.AddedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListMemberProjectIdsAsync(Guid userId, CancellationToken ct)
    {
        return await db.Set<Project>().AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .Select(p => p.Id)
            .ToListAsync(ct);
    }
}
