// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Realtime;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Endpoints;

internal static class IssuesEndpoints
{
    internal sealed record CreateIssueRequest(
        string Title, string? Description,
        IssueType? Type, int? Points, string? AcceptanceCriteria, Guid? AssigneeId);
    internal sealed record EditIssueRequest(
        string Title, string? Description,
        IssueType Type, int? Points, string? AcceptanceCriteria,
        Guid? AssigneeId, Guid ReporterId);
    internal sealed record TransitionRequest(IssueStatus Status);
    internal sealed record AddCommentRequest(string Body, Guid AuthorId);
    internal sealed record BeginUploadEndpointRequest(string FileName, string? ContentType, long SizeBytes);
    internal sealed record IssueDto(
        Guid Id,
        Guid ProjectId,
        string ProjectKey,
        int Number,
        string Title,
        string? Description,
        IssueStatus Status,
        IssueType Type,
        int? Points,
        string? AcceptanceCriteria,
        Guid? AssigneeId,
        Guid ReporterId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects/{projectId:guid}/issues", async (
            Guid projectId,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            CancellationToken ct) =>
        {
            var project = await projects.GetSummaryAsync(projectId, ct);
            if (project is null) return Results.NotFound();

            var items = await db.Set<Issue>()
                .AsNoTracking()
                .Where(i => i.ProjectId == projectId)
                .OrderBy(i => i.Number)
                .Select(i => new IssueDto(
                    i.Id, i.ProjectId, project.Key, i.Number, i.Title, i.Description,
                    i.Status, i.Type, i.Points, i.AcceptanceCriteria, i.AssigneeId, i.ReporterId,
                    i.CreatedAt, i.UpdatedAt))
                .ToListAsync(ct);
            return Results.Ok(items);
        })
        .WithName("ListIssues")
        .WithSummary("List issues for a project")
        .WithTags("Issues")
        .Produces<List<IssueDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/projects/{projectId:guid}/issues", async (
            Guid projectId,
            [FromBody] CreateIssueRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.BadRequest(new { error = "Title is required." });

            var project = await projects.GetSummaryAsync(projectId, ct);
            if (project is null) return Results.NotFound();

            var number = await projects.AllocateNextIssueNumberAsync(projectId, ct);
            if (number is null) return Results.NotFound();

            // TODO: resolve real CreatedById from auth context once Identity UI lands.
            var createdBy = Guid.Empty;

            var issue = Issue.Create(project.Id, number.Value, req.Title, req.Description, createdBy, clock.UtcNow);
            issue.Reclassify(req.Type ?? IssueType.Task, req.Points, req.AcceptanceCriteria, clock.UtcNow);
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
                issue.Status, issue.Type, issue.Points, issue.AcceptanceCriteria,
                issue.AssigneeId, issue.ReporterId, issue.CreatedAt, issue.UpdatedAt));
        })
        .WithName("CreateIssue")
        .WithSummary("Create a new issue in a project")
        .WithTags("Issues")
        .Produces<IssueDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapGet("/api/issues/{id:guid}", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var project = await projects.GetSummaryAsync(issue.ProjectId, ct);
            if (project is null) return Results.NotFound();

            return Results.Ok(new IssueDto(
                issue.Id, issue.ProjectId, project.Key, issue.Number, issue.Title, issue.Description,
                issue.Status, issue.Type, issue.Points, issue.AcceptanceCriteria,
                issue.AssigneeId, issue.ReporterId, issue.CreatedAt, issue.UpdatedAt));
        })
        .WithName("GetIssue")
        .WithSummary("Get a single issue by id")
        .WithTags("Issues")
        .Produces<IssueDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/issues/{id:guid}", async (
            Guid id,
            [FromBody] EditIssueRequest req,
            [FromServices] DbContext db,
            [FromServices] IClock clock,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            issue.Edit(req.Title, req.Description, clock.UtcNow);
            issue.Reclassify(req.Type, req.Points, req.AcceptanceCriteria, clock.UtcNow);
            issue.Assign(req.AssigneeId, clock.UtcNow);
            if (req.ReporterId != Guid.Empty)
                issue.SetReporter(req.ReporterId, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("EditIssue")
        .WithSummary("Edit an issue's title or description")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPatch("/api/issues/{id:guid}/status", async (
            Guid id,
            [FromBody] TransitionRequest req,
            [FromServices] DbContext db,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            [FromServices] IRealtimeNotifier realtime,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
            var oldStatus = issue.Status;
            issue.Transition(req.Status, clock.UtcNow);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "issue.transitioned",
                ResourceType: "Issue",
                ResourceId: issue.Id.ToString(),
                Summary: $"Issue moved {oldStatus} → {issue.Status}",
                Detail: new { from = oldStatus.ToString(), to = issue.Status.ToString() }), ct);

            var groupName = $"tenant:{tenant.Current.Value:N}:project:{issue.ProjectId:N}";
            await realtime.PublishAsync(
                groupName,
                "IssueMoved",
                new { issueId = issue.Id, projectId = issue.ProjectId, status = issue.Status },
                ct);

            return Results.NoContent();
        })
        .WithName("TransitionIssueStatus")
        .WithSummary("Transition an issue between Todo / Doing / Done")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/issues/{id:guid}/comments", async (
            Guid id,
            [FromBody] AddCommentRequest req,
            [FromServices] DbContext db,
            [FromServices] IClock clock,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Body is required." });

            var issue = await db.Set<Issue>().Include(i => i.Comments).FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();

            var comment = issue.AddComment(req.AuthorId, req.Body, clock.UtcNow);
            // Comment has a domain-assigned Guid key, so EF's "key is set ⇒
            // existing" heuristic would emit UPDATE (0 rows) instead of INSERT.
            // Force the new owned child to Added.
            db.Entry(comment).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/issues/{id}/comments/{comment.Id}", new { comment.Id });
        })
        .WithName("AddIssueComment")
        .WithSummary("Add a comment to an issue")
        .WithTags("Issues")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/issues/{id:guid}", async (
            Guid id,
            [FromServices] DbContext db,
            [FromServices] IAuditLog audit,
            CancellationToken ct) =>
        {
            var issue = await db.Set<Issue>().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (issue is null) return Results.NotFound();
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
        .WithName("DeleteIssue")
        .WithSummary("Delete an issue")
        .WithTags("Issues")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/issues/search", async (
            [FromQuery] string? q,
            [FromQuery] int? limit,
            [FromServices] IIssueSearch search,
            CancellationToken ct) =>
        {
            var hits = await search.SearchAsync(q ?? string.Empty, limit ?? 20, ct);
            return Results.Ok(hits);
        })
        .WithName("SearchIssues")
        .WithSummary("Full-text search across issue titles and descriptions")
        .WithTags("Search")
        .Produces<IReadOnlyList<IssueSearchHit>>(StatusCodes.Status200OK);

        app.MapGet("/api/issues/{id:guid}/attachments", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            CancellationToken ct) =>
        {
            var list = await attachments.ListAsync(id, ct);
            return Results.Ok(list);
        })
        .WithName("ListIssueAttachments")
        .WithSummary("List attachments on an issue")
        .WithTags("Attachments")
        .Produces<IReadOnlyList<AttachmentSummary>>(StatusCodes.Status200OK);

        app.MapPost("/api/issues/{issueId:guid}/attachments/begin", async (
            Guid issueId,
            [FromBody] BeginUploadEndpointRequest req,
            [FromServices] IAttachmentService attachments,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.FileName))
                return Results.BadRequest(new { error = "fileName is required." });
            if (req.SizeBytes < 0)
                return Results.BadRequest(new { error = "sizeBytes must be non-negative." });

            var result = await attachments.BeginUploadAsync(
                issueId, req.FileName, req.ContentType ?? "application/octet-stream",
                req.SizeBytes, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("BeginAttachmentUpload")
        .WithSummary("Begin a pre-signed direct browser upload — returns the PUT URL + metadata")
        .WithTags("Attachments")
        .Produces<BeginUploadResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();

        app.MapPost("/api/issues/{issueId:guid}/attachments/{attachmentId:guid}/complete", async (
            Guid issueId,
            Guid attachmentId,
            [FromBody] CompleteUploadRequest req,
            [FromServices] IAttachmentService attachments,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var uploadedBy))
                return Results.Unauthorized();

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
        .WithSummary("Phase 2: confirm a pre-signed upload completed and persist metadata")
        .WithTags("Attachments")
        .Produces<AttachmentSummary>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/attachments/{id:guid}/download-url", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            CancellationToken ct) =>
        {
            var url = await attachments.GetDownloadUrlAsync(id, TimeSpan.FromMinutes(15), ct);
            return url is null
                ? Results.NotFound()
                : Results.Ok(new { url = url.ToString() });
        })
        .WithName("GetAttachmentDownloadUrl")
        .WithSummary("Get a 15-minute pre-signed GET URL for an attachment")
        .WithTags("Attachments")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/attachments/{id:guid}", async (
            Guid id,
            [FromServices] IAttachmentService attachments,
            CancellationToken ct) =>
        {
            return await attachments.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .WithName("DeleteAttachment")
        .WithSummary("Delete an attachment (best-effort object removal + metadata removal)")
        .WithTags("Attachments")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
