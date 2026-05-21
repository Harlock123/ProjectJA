// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Realtime;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Endpoints;

internal static class IssuesEndpoints
{
    internal sealed record CreateIssueRequest(
        string Title, string? Description,
        IssueType? Type, IssuePriority? Priority, int? Points, string? AcceptanceCriteria,
        Guid? AssigneeId, IReadOnlyList<string>? Labels);
    internal sealed record EditIssueRequest(
        string Title, string? Description,
        IssueType Type, IssuePriority Priority, int? Points, string? AcceptanceCriteria,
        Guid? AssigneeId, Guid ReporterId, IReadOnlyList<string>? Labels);
    internal sealed record TransitionRequest(Guid WorkflowStateId);
    internal sealed record AssignSprintRequest(Guid? SprintId);
    internal sealed record AddCommentRequest(string Body, Guid AuthorId);
    internal sealed record BeginUploadEndpointRequest(string FileName, string? ContentType, long SizeBytes);
    internal sealed record SetScheduleRequest(DateTimeOffset? StartDate, DateTimeOffset? EndDate);
    internal sealed record SetPercentCompleteRequest(int Percent);
    internal sealed record AddBlockerRequest(Guid BlockerIssueId);
    internal sealed record IssueDto(
        Guid Id,
        Guid ProjectId,
        string ProjectKey,
        int Number,
        string Title,
        string? Description,
        Guid WorkflowStateId,
        string WorkflowStateName,
        WorkflowStateCategory WorkflowStateCategory,
        IssueType Type,
        IssuePriority Priority,
        int? Points,
        string? AcceptanceCriteria,
        Guid? AssigneeId,
        Guid ReporterId,
        IReadOnlyList<string> Labels,
        DateTimeOffset? StartDate,
        DateTimeOffset? EndDate,
        int PercentComplete,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/issues", async (
            Guid projectId,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            HttpContext http,
            CancellationToken ct) =>
        {
            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;

            var project = await projects.GetSummaryAsync(projectId, ct);
            if (project is null) return Results.NotFound();

            var workflow = await workflows.GetForProjectAsync(projectId, ct);
            var stateMap = workflow?.States.ToDictionary(s => s.Id) ?? new();

            var raw = await db.Set<Issue>()
                .AsNoTracking()
                .Where(i => i.ProjectId == projectId)
                .OrderBy(i => i.Number)
                .ToListAsync(ct);
            var items = raw.Select(i =>
            {
                var s = stateMap.GetValueOrDefault(i.WorkflowStateId);
                return new IssueDto(
                    i.Id, i.ProjectId, project.Key, i.Number, i.Title, i.Description,
                    i.WorkflowStateId, s?.Name ?? "?", s?.Category ?? WorkflowStateCategory.Open,
                    i.Type, i.Priority, i.Points, i.AcceptanceCriteria, i.AssigneeId, i.ReporterId,
                    i.Labels, i.StartDate, i.EndDate, i.PercentComplete, i.CreatedAt, i.UpdatedAt);
            }).ToList();
            return Results.Ok(items);
        })
        .RequireAuthorization()
        .WithName("ListIssues")
        .WithSummary("List issues for a project (requires membership)")
        .WithTags("Issues")
        .Produces<List<IssueDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/projects/{projectId:guid}/issues", async (
            Guid projectId,
            [FromBody] CreateIssueRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.BadRequest(new { error = "Title is required." });

            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            var createdBy = auth.UserId;

            var project = await projects.GetSummaryAsync(projectId, ct);
            if (project is null) return Results.NotFound();

            // The initial state is the first Open-category state by Order in the
            // project's workflow (matches the old "Todo" default). If there's
            // somehow no Open state, fall back to the first state overall.
            var wf = await workflows.GetForProjectAsync(projectId, ct);
            if (wf is null || wf.States.Count == 0)
                return Results.BadRequest(new { error = "Project has no workflow configured." });
            var initialState = wf.States.OrderBy(s => s.Order)
                .FirstOrDefault(s => s.Category == WorkflowStateCategory.Open)
                ?? wf.States.OrderBy(s => s.Order).First();

            var number = await projects.AllocateNextIssueNumberAsync(projectId, ct);
            if (number is null) return Results.NotFound();

            var issue = Issue.Create(project.Id, number.Value, req.Title, req.Description,
                initialState.Id, createdBy, clock.UtcNow);
            issue.Reclassify(req.Type ?? IssueType.Task, req.Points, req.AcceptanceCriteria, clock.UtcNow);
            issue.SetPriority(req.Priority ?? IssuePriority.Medium, clock.UtcNow);
            if (req.Labels is not null)
                issue.SetLabels(req.Labels, clock.UtcNow);
            if (req.AssigneeId is not null)
                issue.Assign(req.AssigneeId, clock.UtcNow);
            db.Set<Issue>().Add(issue);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "issue.created",
                ResourceType: "Issue",
                ResourceId: issue.Id.ToString(),
                Summary: $"{project.Key}-{issue.Number} created: {issue.Title}",
                Detail: new { project.Key, issue.Number, issue.Title }), ct);

            return Results.Created($"/api/issues/{issue.Id}", new IssueDto(
                issue.Id, issue.ProjectId, project.Key, issue.Number, issue.Title, issue.Description,
                issue.WorkflowStateId, initialState.Name, initialState.Category,
                issue.Type, issue.Priority, issue.Points, issue.AcceptanceCriteria,
                issue.AssigneeId, issue.ReporterId, issue.Labels,
                issue.StartDate, issue.EndDate, issue.PercentComplete,
                issue.CreatedAt, issue.UpdatedAt));
        })
        .RequireAuthorization()
        .WithName("CreateIssue")
        .WithSummary("Create a new issue in a project (requires Member role)")
        .WithTags("Issues")
        .Produces<IssueDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapGet("/api/issues/{id:guid}", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;
            var project = await projects.GetSummaryAsync(issue.ProjectId, ct);
            if (project is null) return Results.NotFound();
            var state = await workflows.GetStateAsync(issue.WorkflowStateId, ct);

            return Results.Ok(new IssueDto(
                issue.Id, issue.ProjectId, project.Key, issue.Number, issue.Title, issue.Description,
                issue.WorkflowStateId, state?.Name ?? "?", state?.Category ?? WorkflowStateCategory.Open,
                issue.Type, issue.Priority, issue.Points, issue.AcceptanceCriteria,
                issue.AssigneeId, issue.ReporterId, issue.Labels,
                issue.StartDate, issue.EndDate, issue.PercentComplete,
                issue.CreatedAt, issue.UpdatedAt));
        })
        .RequireAuthorization()
        .WithName("GetIssue")
        .WithSummary("Get a single issue by id (requires membership)")
        .WithTags("Issues")
        .Produces<IssueDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/issues/{id:guid}", async (
            Guid id,
            [FromBody] EditIssueRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            issue.Edit(req.Title, req.Description, clock.UtcNow);
            issue.Reclassify(req.Type, req.Points, req.AcceptanceCriteria, clock.UtcNow);
            issue.SetPriority(req.Priority, clock.UtcNow);
            issue.SetLabels(req.Labels ?? Array.Empty<string>(), clock.UtcNow);
            issue.Assign(req.AssigneeId, clock.UtcNow);
            if (req.ReporterId != Guid.Empty)
                issue.SetReporter(req.ReporterId, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("EditIssue")
        .WithSummary("Edit an issue (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/issues/{id:guid}/status", async (
            Guid id,
            [FromBody] TransitionRequest req,
            [FromServices] DbContext db,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            [FromServices] IRealtimeNotifier realtime,
            [FromServices] ITenantContext tenant,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;

            // The target state must belong to this issue's project's workflow.
            var wf = await workflows.GetForProjectAsync(issue.ProjectId, ct);
            var target = wf?.States.FirstOrDefault(s => s.Id == req.WorkflowStateId);
            if (target is null)
                return Results.BadRequest(new { error = "Target state is not in this project's workflow." });

            var oldStateId = issue.WorkflowStateId;
            var oldState = wf!.States.FirstOrDefault(s => s.Id == oldStateId);
            // Enforce the workflow's transition matrix. Same-state is OK
            // (no-op, handled by IsTransitionAllowed) — useful for idempotent
            // PATCHes.
            if (!wf.IsTransitionAllowed(oldStateId, req.WorkflowStateId))
                return Results.Conflict(new { error = $"Workflow doesn't allow moving from \"{oldState?.Name ?? "?"}\" to \"{target.Name}\"." });

            issue.Transition(req.WorkflowStateId, clock.UtcNow);
            // Moving into any Done-category state implies the work is finished.
            // Bump to 100% so the report / Excel export stays consistent. We
            // never reset progress on reverse transitions (would silently drop
            // user-entered data).
            if (target.Category == WorkflowStateCategory.Done)
                issue.MarkComplete(clock.UtcNow);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "issue.transitioned",
                ResourceType: "Issue",
                ResourceId: issue.Id.ToString(),
                Summary: $"Issue moved {oldState?.Name ?? "?"} → {target.Name}",
                Detail: new { fromStateId = oldStateId, from = oldState?.Name, toStateId = target.Id, to = target.Name }), ct);

            var groupName = $"tenant:{tenant.Current.Value:N}:project:{issue.ProjectId:N}";
            await realtime.PublishAsync(
                groupName,
                "IssueMoved",
                new { issueId = issue.Id, projectId = issue.ProjectId, workflowStateId = issue.WorkflowStateId },
                ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("TransitionIssueStatus")
        .WithSummary("Transition an issue to a different workflow state (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // Assign/clear sprint on an issue — Admin only (matches sprint authority).
        // Null sprintId moves the issue back to backlog. The target sprint must
        // be in the same project as the issue and not Completed.
        app.MapPatch("/api/issues/{id:guid}/sprint", async (
            Guid id,
            [FromBody] AssignSprintRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] ISprintQueries sprintQueries,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            if (req.SprintId is { } sid)
            {
                var target = await sprintQueries.GetByIdAsync(sid, ct);
                if (target is null)
                    return Results.BadRequest(new { error = "Sprint not found." });
                if (target.ProjectId != issue.ProjectId)
                    return Results.BadRequest(new { error = "Sprint belongs to a different project." });
                if (target.Status == SprintStatus.Completed)
                    return Results.Conflict(new { error = "Can't assign an issue to a completed sprint." });
            }

            var previous = issue.SprintId;
            issue.AssignToSprint(req.SprintId, clock.UtcNow);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "issue.sprint_changed",
                ResourceType: "Issue",
                ResourceId: issue.Id.ToString(),
                Summary: req.SprintId is null
                    ? $"Moved issue back to backlog (was sprint {previous})"
                    : $"Moved issue to sprint {req.SprintId} (was {(previous?.ToString() ?? "backlog")})",
                Detail: new { issueId = issue.Id, projectId = issue.ProjectId, previousSprintId = previous, newSprintId = req.SprintId }), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("AssignIssueSprint")
        .WithSummary("Move an issue into a sprint or back to backlog (Admin only)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        app.MapPatch("/api/issues/{id:guid}/schedule", async (
            Guid id,
            [FromBody] SetScheduleRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            try { issue.SetSchedule(req.StartDate, req.EndDate, clock.UtcNow); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("SetIssueSchedule")
        .WithSummary("Set planned start/end dates on an issue (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/issues/{id:guid}/percent-complete", async (
            Guid id,
            [FromBody] SetPercentCompleteRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            try { issue.SetPercentComplete(req.Percent, clock.UtcNow); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("SetIssuePercentComplete")
        .WithSummary("Set manual % complete (0–100) on an issue (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/issues/{id:guid}/links", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IIssueLinkService links,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;
            return Results.Ok(await links.GetLinksAsync(id, ct));
        })
        .RequireAuthorization()
        .WithName("GetIssueLinks")
        .WithSummary("List the issues blocking this one and the issues it blocks (requires membership)")
        .WithTags("Issues")
        .Produces<IssueLinksView>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST adds "blockerIssue blocks {id}" — i.e. registers a new entry in
        // this issue's BlockedBy list. Validation lives in IIssueLinkService;
        // we map its discriminated outcome to HTTP semantics here.
        app.MapPost("/api/issues/{id:guid}/blockers", async (
            Guid id,
            [FromBody] AddBlockerRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IIssueLinkService links,
            HttpContext http,
            CancellationToken ct) =>
        {
            var blocked = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (blocked is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, blocked.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;

            var outcome = await links.AddBlockerAsync(id, req.BlockerIssueId, auth.UserId, ct);
            return outcome switch
            {
                AddBlockerOutcome.Added => Results.Created($"/api/issues/{id}/links", new { }),
                AddBlockerOutcome.SelfLink => Results.BadRequest(new { error = "An issue cannot block itself." }),
                AddBlockerOutcome.NotFound => Results.BadRequest(new { error = "Blocker issue not found." }),
                AddBlockerOutcome.CrossProject => Results.BadRequest(new { error = "Blocker must belong to the same project." }),
                AddBlockerOutcome.Duplicate => Results.Conflict(new { error = "Link already exists." }),
                AddBlockerOutcome.Cycle => Results.Conflict(new { error = "Adding this blocker would create a cycle." }),
                _ => Results.Problem("Unknown outcome."),
            };
        })
        .RequireAuthorization()
        .WithName("AddIssueBlocker")
        .WithSummary("Register that another issue blocks this one (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        app.MapDelete("/api/issues/{id:guid}/blockers/{blockerIssueId:guid}", async (
            Guid id,
            Guid blockerIssueId,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IIssueLinkService links,
            HttpContext http,
            CancellationToken ct) =>
        {
            var blocked = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (blocked is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, blocked.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            return await links.RemoveBlockerAsync(id, blockerIssueId, ct)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("RemoveIssueBlocker")
        .WithSummary("Remove a blocker relationship (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/issues/{id:guid}/comments", async (
            Guid id,
            [FromBody] AddCommentRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Body is required." });

            var issue = await db.Set<Issue>().Include(i => i.Comments).FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;

            var comment = issue.AddComment(req.AuthorId, req.Body, clock.UtcNow);
            // Comment has a domain-assigned Guid key, so EF's "key is set ⇒
            // existing" heuristic would emit UPDATE (0 rows) instead of INSERT.
            // Force the new owned child to Added.
            db.Entry(comment).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/issues/{id}/comments/{comment.Id}", new { comment.Id });
        })
        .RequireAuthorization()
        .WithName("AddIssueComment")
        .WithSummary("Add a comment to an issue (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/issues/{id:guid}", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IAuditLog audit,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, issue.ProjectId, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            var summary = $"Issue {issue.Id} deleted (was \"{issue.Title}\")";
            db.Set<Issue>().Remove(issue);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "issue.deleted",
                ResourceType: "Issue",
                ResourceId: issue.Id.ToString(),
                Summary: summary), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeleteIssue")
        .WithSummary("Delete an issue (requires Member role)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/projects/{projectId:guid}/export.xlsx", async (
            Guid projectId,
            [FromServices] IIssueExportService export,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;
            var bytes = await export.ExportProjectAsync(projectId, ct);
            if (bytes is null) return Results.NotFound();
            var summary = await projects.GetSummaryAsync(projectId, ct);
            var fileName = $"{summary?.Key ?? "project"}-issues.xlsx";
            return Results.File(bytes,
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileDownloadName: fileName);
        })
        .RequireAuthorization()
        .WithName("ExportProjectIssues")
        .WithSummary("Download an MS-Project-style xlsx of every issue in a project (requires membership)")
        .WithTags("Issues")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/issues/search", async (
            [FromQuery] string? q,
            [FromQuery] int? limit,
            [FromServices] IIssueSearch search,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var claim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(claim, out var userId))
                return Results.Unauthorized();

            var hits = await search.SearchAsync(q ?? string.Empty, limit ?? 20, ct);
            // Post-filter to the caller's member projects (the FTS query spans the
            // whole tenant; gating in-SQL would mean touching the net10-fragile
            // PlainToTsQuery expression — deliberately kept out of scope).
            var allowed = new HashSet<Guid>(await projects.ListMemberProjectIdsAsync(userId, ct));
            var visible = hits.Where(h => allowed.Contains(h.ProjectId)).ToList();
            return Results.Ok(visible);
        })
        .RequireAuthorization()
        .WithName("SearchIssues")
        .WithSummary("Full-text search across issues in projects you're a member of")
        .WithTags("Search")
        .Produces<IReadOnlyList<IssueSearchHit>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/issues/{id:guid}/attachments", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var listProjectId = await db.Set<Issue>().AsNoTracking()
                .Where(i => i.Id == id).Select(i => (Guid?)i.ProjectId).FirstOrDefaultAsync(ct);
            if (listProjectId is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, listProjectId.Value, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;

            var list = await attachments.ListAsync(id, ct);
            return Results.Ok(list);
        })
        .RequireAuthorization()
        .WithName("ListIssueAttachments")
        .WithSummary("List attachments on an issue (requires membership)")
        .WithTags("Attachments")
        .Produces<IReadOnlyList<AttachmentSummary>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/issues/{issueId:guid}/attachments/begin", async (
            Guid issueId,
            [FromBody] BeginUploadEndpointRequest req,
            [FromServices] IAttachmentService attachments,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.FileName))
                return Results.BadRequest(new { error = "fileName is required." });
            if (req.SizeBytes < 0)
                return Results.BadRequest(new { error = "sizeBytes must be non-negative." });

            var beginProjectId = await db.Set<Issue>().AsNoTracking()
                .Where(i => i.Id == issueId).Select(i => (Guid?)i.ProjectId).FirstOrDefaultAsync(ct);
            if (beginProjectId is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, beginProjectId.Value, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;

            var result = await attachments.BeginUploadAsync(
                issueId, req.FileName, req.ContentType ?? "application/octet-stream",
                req.SizeBytes, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("BeginAttachmentUpload")
        .WithSummary("Begin a pre-signed direct browser upload (requires Member role)")
        .WithTags("Attachments")
        .Produces<BeginUploadResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapPost("/api/issues/{issueId:guid}/attachments/{attachmentId:guid}/complete", async (
            Guid issueId,
            Guid attachmentId,
            [FromBody] CompleteUploadRequest req,
            [FromServices] IAttachmentService attachments,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var completeProjectId = await db.Set<Issue>().AsNoTracking()
                .Where(i => i.Id == issueId).Select(i => (Guid?)i.ProjectId).FirstOrDefaultAsync(ct);
            if (completeProjectId is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, completeProjectId.Value, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;
            var uploadedBy = auth.UserId;

            try
            {
                var summary = await attachments.CompleteUploadAsync(issueId, attachmentId, req, uploadedBy, ct);
                return summary is null
                    ? Results.NotFound()
                    : Results.Created($"/api/attachments/{summary.Id}", summary);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization()
        .WithName("CompleteAttachmentUpload")
        .WithSummary("Phase 2: confirm a pre-signed upload completed (requires Member role)")
        .WithTags("Attachments")
        .Produces<AttachmentSummary>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/attachments/{id:guid}/download-url", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var dlIssueId = await db.Set<Attachment>().AsNoTracking()
                .Where(a => a.Id == id).Select(a => (Guid?)a.IssueId).FirstOrDefaultAsync(ct);
            if (dlIssueId is null) return Results.NotFound();
            var dlProjectId = await db.Set<Issue>().AsNoTracking()
                .Where(i => i.Id == dlIssueId.Value).Select(i => (Guid?)i.ProjectId).FirstOrDefaultAsync(ct);
            if (dlProjectId is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, dlProjectId.Value, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;

            var url = await attachments.GetDownloadUrlAsync(id, TimeSpan.FromMinutes(15), ct);
            return url is null
                ? Results.NotFound()
                : Results.Ok(new { url = url.ToString() });
        })
        .RequireAuthorization()
        .WithName("GetAttachmentDownloadUrl")
        .WithSummary("Get a 15-minute pre-signed GET URL for an attachment (requires membership)")
        .WithTags("Attachments")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/attachments/{id:guid}", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            HttpContext http,
            CancellationToken ct) =>
        {
            var attIssueId = await db.Set<Attachment>().AsNoTracking()
                .Where(a => a.Id == id).Select(a => (Guid?)a.IssueId).FirstOrDefaultAsync(ct);
            if (attIssueId is null) return Results.NotFound();
            var attProjectId = await db.Set<Issue>().AsNoTracking()
                .Where(i => i.Id == attIssueId.Value).Select(i => (Guid?)i.ProjectId).FirstOrDefaultAsync(ct);
            if (attProjectId is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, attProjectId.Value, ProjectRole.Member, ct);
            if (auth.Denied) return auth.Failure!;

            return await attachments.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .RequireAuthorization()
        .WithName("DeleteAttachment")
        .WithSummary("Delete an attachment (requires Member role)")
        .WithTags("Attachments")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
