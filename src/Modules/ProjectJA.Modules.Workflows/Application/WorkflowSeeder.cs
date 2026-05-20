// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Workflows.Application;

internal sealed class WorkflowSeeder(DbContext db) : IWorkflowSeeder
{
    public async Task<Guid> EnsureDefaultForProjectAsync(Guid projectId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Set<Workflow>()
            .Where(w => w.ProjectId == projectId)
            .OrderBy(w => w.CreatedAt)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing.Value;

        var wf = Workflow.CreateDefault(projectId, now);
        db.Set<Workflow>().Add(wf);
        // Owned states with domain-assigned Guid keys: EF would otherwise
        // misclassify them as Modified and emit UPDATEs (the same trap that
        // bit Comments + ProjectMember). Force-Added on each child fixes it.
        foreach (var state in wf.States)
            db.Entry(state).State = EntityState.Added;
        await db.SaveChangesAsync(ct);
        return wf.Id;
    }
}
