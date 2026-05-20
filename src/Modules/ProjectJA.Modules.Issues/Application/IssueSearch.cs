// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Issues.Persistence;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class IssueSearch(DbContext db, IProjectQueries projects, IWorkflowQueries workflows) : IIssueSearch
{
    public async Task<IReadOnlyList<IssueSearchHit>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<IssueSearchHit>();

        var cap = Math.Clamp(limit, 1, 100);

        // EF.Functions.PlainToTsQuery must be called *inside* the query expression
        // tree — EF Core 10 / Npgsql 10 client-evaluates (and rejects) it if it's
        // pre-computed into a local first. Repeating the call is fine; it maps to
        // the same plainto_tsquery() SQL each time.
        var rows = await db.Set<Issue>()
            .AsNoTracking()
            .Where(i => EF.Property<NpgsqlTsVector>(i, IssueConfiguration.SearchVectorShadowProperty)
                        .Matches(EF.Functions.PlainToTsQuery("english", query)))
            .OrderByDescending(i => EF.Property<NpgsqlTsVector>(i, IssueConfiguration.SearchVectorShadowProperty)
                        .Rank(EF.Functions.PlainToTsQuery("english", query)))
            .Take(cap)
            .Select(i => new
            {
                i.Id, i.ProjectId, i.Number, i.Title, i.Description, i.WorkflowStateId,
                Rank = (double)EF.Property<NpgsqlTsVector>(i, IssueConfiguration.SearchVectorShadowProperty)
                            .Rank(EF.Functions.PlainToTsQuery("english", query))
            })
            .ToListAsync(ct);

        var projectKeys = new Dictionary<Guid, string>();
        foreach (var pid in rows.Select(r => r.ProjectId).Distinct())
        {
            var summary = await projects.GetSummaryAsync(pid, ct);
            if (summary is not null) projectKeys[pid] = summary.Key;
        }

        // Resolve each state once, cached locally. State count is small
        // (≤ workflow size × project count among the hits); not worth
        // batching further.
        var stateLookup = new Dictionary<Guid, WorkflowStateView>();
        foreach (var sid in rows.Select(r => r.WorkflowStateId).Distinct())
        {
            var s = await workflows.GetStateAsync(sid, ct);
            if (s is not null) stateLookup[sid] = s;
        }

        return rows.Select(r =>
        {
            var s = stateLookup.GetValueOrDefault(r.WorkflowStateId);
            return new IssueSearchHit(
                r.Id, r.ProjectId,
                projectKeys.GetValueOrDefault(r.ProjectId, "?"),
                r.Number, r.Title, r.Description,
                r.WorkflowStateId,
                s?.Name ?? "?",
                s?.Category ?? WorkflowStateCategory.Open,
                r.Rank);
        }).ToList();
    }
}
