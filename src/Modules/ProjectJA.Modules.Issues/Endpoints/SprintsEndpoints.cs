// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

// Lives in the Issues module (despite Sprint living in Projects.Domain) because
// the Complete + Delete endpoints need Issue access via ISprintIssueOps — the
// module dependency runs Issues → Projects only, never the other way.
namespace ProjectJA.Modules.Issues.Endpoints;

internal static class SprintsEndpoints
{
    internal sealed record CreateSprintRequest(
        string Name, string? Goal,
        DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd);

    internal sealed record EditSprintRequest(
        string Name, string? Goal,
        DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd);

    internal sealed record SprintDto(
        Guid Id, Guid ProjectId,
        string Name, string? Goal,
        SprintStatus Status,
        DateTimeOffset? PlannedStart, DateTimeOffset? PlannedEnd,
        DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
        DateTimeOffset CreatedAt);

    private static SprintDto ToDto(SprintSummary s) => new(
        s.Id, s.ProjectId, s.Name, s.Goal, s.Status,
        s.PlannedStart, s.PlannedEnd, s.StartedAt, s.CompletedAt, s.CreatedAt);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        // List — any member can see the sprints in a project.
        app.MapGet("/api/projects/{projectId:guid}/sprints", async (
            Guid projectId,
            [FromServices] IProjectQueries projects,
            [FromServices] ISprintQueries sprints,
            HttpContext http,
            CancellationToken ct) =>
        {
            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;

            var list = await sprints.ListForProjectAsync(projectId, ct);
            return Results.Ok(list.Select(ToDto));
        })
        .RequireAuthorization()
        .WithName("ListSprints")
        .WithSummary("List sprints in a project (requires membership)")
        .WithTags("Sprints")
        .Produces<List<SprintDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Create — Admin only. Sprint starts Planned.
        app.MapPost("/api/projects/{projectId:guid}/sprints", async (
            Guid projectId,
            [FromBody] CreateSprintRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Sprint name is required." });

            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            var sprint = Sprint.Create(projectId, req.Name, req.Goal, req.PlannedStart, req.PlannedEnd, clock.UtcNow);
            db.Set<Sprint>().Add(sprint);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "sprint.created",
                ResourceType: "Project",
                ResourceId: projectId.ToString(),
                Summary: $"Created sprint \"{sprint.Name}\"",
                Detail: new { sprintId = sprint.Id, sprint.Name, sprint.Goal, sprint.PlannedStart, sprint.PlannedEnd }), ct);

            return Results.Created($"/api/sprints/{sprint.Id}", ToDto(new SprintSummary(
                sprint.Id, sprint.ProjectId, sprint.Name, sprint.Goal, sprint.Status,
                sprint.PlannedStart, sprint.PlannedEnd, sprint.StartedAt, sprint.CompletedAt, sprint.CreatedAt)));
        })
        .RequireAuthorization()
        .WithName("CreateSprint")
        .WithSummary("Create a sprint (Admin only)")
        .WithTags("Sprints")
        .Produces<SprintDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Edit — Admin only. Domain rejects edits on a Completed sprint.
        app.MapPut("/api/sprints/{id:guid}", async (
            Guid id,
            [FromBody] EditSprintRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            var sprint = await db.Set<Sprint>().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (sprint is null) return Results.NotFound();

            var auth = await ProjectAccess.RequireAsync(http, projects, sprint.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            try
            {
                sprint.Edit(req.Name, req.Goal, req.PlannedStart, req.PlannedEnd);
                await db.SaveChangesAsync(ct);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }

            await audit.RecordAsync(new AuditEntry(
                Action: "sprint.edited",
                ResourceType: "Project",
                ResourceId: sprint.ProjectId.ToString(),
                Summary: $"Edited sprint \"{sprint.Name}\"",
                Detail: new { sprintId = sprint.Id, sprint.Name, sprint.Goal, sprint.PlannedStart, sprint.PlannedEnd }), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("EditSprint")
        .WithSummary("Edit a sprint's name / goal / planned dates (Admin only)")
        .WithTags("Sprints")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // Start — Admin only. Enforces one-Active-per-project at the endpoint
        // (the domain just gates Planned→Active).
        app.MapPatch("/api/sprints/{id:guid}/start", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] ISprintQueries sprintQueries,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var sprint = await db.Set<Sprint>().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (sprint is null) return Results.NotFound();

            var auth = await ProjectAccess.RequireAsync(http, projects, sprint.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            if (await sprintQueries.HasActiveSprintAsync(sprint.ProjectId, ct))
                return Results.Conflict(new { error = "Another sprint is already Active in this project. Complete it first." });

            try { sprint.Start(clock.UtcNow); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "sprint.started",
                ResourceType: "Project",
                ResourceId: sprint.ProjectId.ToString(),
                Summary: $"Started sprint \"{sprint.Name}\"",
                Detail: new { sprintId = sprint.Id, sprint.Name, startedAt = sprint.StartedAt }), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("StartSprint")
        .WithSummary("Start a Planned sprint (Admin only; only one Active per project)")
        .WithTags("Sprints")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // Complete — Admin only. Returns incomplete issues to the backlog.
        app.MapPatch("/api/sprints/{id:guid}/complete", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] ISprintIssueOps issueOps,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            var sprint = await db.Set<Sprint>().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (sprint is null) return Results.NotFound();

            var auth = await ProjectAccess.RequireAsync(http, projects, sprint.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            try { sprint.Complete(clock.UtcNow); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);

            // Push any still-open issues back to the backlog (separate context-
            // free operation via the Issues bridge; happens after the sprint
            // status flip so a partial failure leaves the sprint Completed and
            // the issues correctly cleared on retry — idempotent.)
            var moved = await issueOps.ClearSprintForIncompleteAsync(sprint.Id, clock.UtcNow, ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "sprint.completed",
                ResourceType: "Project",
                ResourceId: sprint.ProjectId.ToString(),
                Summary: $"Completed sprint \"{sprint.Name}\" ({moved} issue(s) returned to backlog)",
                Detail: new { sprintId = sprint.Id, sprint.Name, completedAt = sprint.CompletedAt, issuesReturnedToBacklog = moved }), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("CompleteSprint")
        .WithSummary("Complete an Active sprint — open issues return to backlog (Admin only)")
        .WithTags("Sprints")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // Delete — Admin only. Allowed only when Planned AND no issues assigned.
        app.MapDelete("/api/sprints/{id:guid}", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] ISprintIssueOps issueOps,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            var sprint = await db.Set<Sprint>().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (sprint is null) return Results.NotFound();

            var auth = await ProjectAccess.RequireAsync(http, projects, sprint.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            if (sprint.Status != SprintStatus.Planned)
                return Results.Conflict(new { error = "Only a Planned sprint can be deleted. Complete the active sprint first." });
            if (await issueOps.HasAnyIssuesInSprintAsync(sprint.Id, ct))
                return Results.Conflict(new { error = "Sprint still has assigned issues. Move them to the backlog first." });

            var name = sprint.Name;
            var projectId = sprint.ProjectId;
            db.Set<Sprint>().Remove(sprint);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "sprint.deleted",
                ResourceType: "Project",
                ResourceId: projectId.ToString(),
                Summary: $"Deleted sprint \"{name}\"",
                Detail: new { sprintId = id, name }), ct);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeleteSprint")
        .WithSummary("Delete a Planned, empty sprint (Admin only)")
        .WithTags("Sprints")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
