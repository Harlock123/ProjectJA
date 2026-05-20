// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Workflows.Application;

internal sealed class WorkflowQueries(DbContext db) : IWorkflowQueries
{
    public async Task<WorkflowView?> GetForProjectAsync(Guid projectId, CancellationToken ct)
    {
        // Only one workflow per project in this slice; if more than one exists,
        // pick the oldest (the system-seeded default) — defensive ordering.
        var wf = await db.Set<Workflow>().AsNoTracking()
            .Where(w => w.ProjectId == projectId)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (wf is null) return null;

        return new WorkflowView(
            wf.Id, wf.ProjectId, wf.Name, wf.IsDefault, wf.CreatedAt,
            wf.States.OrderBy(s => s.Order)
                .Select(s => new WorkflowStateView(s.Id, s.Name, s.Order, s.Category))
                .ToList(),
            wf.Transitions
                .Select(t => new WorkflowTransitionView(t.FromStateId, t.ToStateId))
                .ToList());
    }

    public async Task<WorkflowStateView?> GetStateAsync(Guid stateId, CancellationToken ct)
    {
        var state = await db.Set<Workflow>().AsNoTracking()
            .SelectMany(w => w.States)
            .Where(s => s.Id == stateId)
            .Select(s => new WorkflowStateView(s.Id, s.Name, s.Order, s.Category))
            .FirstOrDefaultAsync(ct);
        return state;
    }

    public async Task<bool> HasIssueInStateAsync(Guid stateId, CancellationToken ct)
    {
        // Plain SQL via the model's table name keeps Workflows module free of an
        // Issues reference. The Issue type lives in another module; we don't
        // need its CLR type here — only the column.
        var count = await db.Database
            .SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM issues WHERE "WorkflowStateId" = {stateId}""")
            .FirstAsync(ct);
        return count > 0;
    }
}
