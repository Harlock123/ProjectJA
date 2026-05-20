// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

// Lives in the Projects module — needs both `ProjectAccess`/`IProjectQueries`
// (Projects.Contracts) and the `Workflow` domain (Workflows.Domain). The
// dependency direction is Projects → Workflows, so this is the side with
// visibility of both. (Same pattern as SprintsEndpoints in the Issues module.)
namespace ProjectJA.Modules.Projects.Endpoints;

internal static class WorkflowsEndpoints
{
    internal sealed record AddStateRequest(string Name, WorkflowStateCategory Category);
    internal sealed record UpdateStateRequest(string Name, WorkflowStateCategory Category);
    internal sealed record ReorderRequest(IReadOnlyList<Guid> StateIds);
    internal sealed record StateDto(Guid Id, string Name, int Order, WorkflowStateCategory Category);
    internal sealed record WorkflowDto(Guid Id, Guid ProjectId, string Name, bool IsDefault, IReadOnlyList<StateDto> States);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        // Get the workflow for a project. Any member can read.
        app.MapGet("/api/projects/{projectId:guid}/workflow", async (
            Guid projectId,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            HttpContext http,
            CancellationToken ct) =>
        {
            var auth = await ProjectAccess.RequireAsync(http, projects, projectId, ProjectRole.Viewer, ct);
            if (auth.Denied) return auth.Failure!;
            var wf = await workflows.GetForProjectAsync(projectId, ct);
            return wf is null
                ? Results.NotFound()
                : Results.Ok(new WorkflowDto(wf.Id, wf.ProjectId, wf.Name, wf.IsDefault,
                    wf.States.Select(s => new StateDto(s.Id, s.Name, s.Order, s.Category)).ToList()));
        })
        .RequireAuthorization()
        .WithName("GetProjectWorkflow")
        .WithSummary("Get the workflow + states for a project (requires membership)")
        .WithTags("Workflows")
        .Produces<WorkflowDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // Add a state — Admin only. Appended at the end of the order.
        app.MapPost("/api/workflows/{workflowId:guid}/states", async (
            Guid workflowId,
            [FromBody] AddStateRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "State name is required." });

            var wf = await db.Set<Workflow>().FirstOrDefaultAsync(w => w.Id == workflowId, ct);
            if (wf is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, wf.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;

            WorkflowState added;
            try { added = wf.AddState(req.Name, req.Category); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            // Owned child with domain-assigned Guid key: force Added so EF emits
            // INSERT (the recurring Comment / ProjectMember / Sprint-state trap).
            db.Entry(added).State = EntityState.Added;
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "workflow.state.added",
                ResourceType: "Project",
                ResourceId: wf.ProjectId.ToString(),
                Summary: $"Added workflow state \"{added.Name}\" ({added.Category})",
                Detail: new { workflowId = wf.Id, stateId = added.Id, added.Name, added.Category, added.Order }), ct);

            return Results.Created($"/api/workflows/{wf.Id}/states/{added.Id}",
                new StateDto(added.Id, added.Name, added.Order, added.Category));
        })
        .RequireAuthorization()
        .WithName("AddWorkflowState")
        .WithSummary("Append a state to a workflow (Admin only)")
        .WithTags("Workflows")
        .Produces<StateDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // Update a state — rename + recategorize — Admin only.
        app.MapPut("/api/workflows/{workflowId:guid}/states/{stateId:guid}", async (
            Guid workflowId,
            Guid stateId,
            [FromBody] UpdateStateRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "State name is required." });
            var wf = await db.Set<Workflow>().FirstOrDefaultAsync(w => w.Id == workflowId, ct);
            if (wf is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, wf.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;
            try
            {
                wf.RenameState(stateId, req.Name);
                wf.RecategorizeState(stateId, req.Category);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.NotFound(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "workflow.state.updated",
                ResourceType: "Project",
                ResourceId: wf.ProjectId.ToString(),
                Summary: $"Updated workflow state {stateId} → \"{req.Name}\" ({req.Category})",
                Detail: new { workflowId = wf.Id, stateId, req.Name, req.Category }), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("UpdateWorkflowState")
        .WithSummary("Rename or recategorize a workflow state (Admin only)")
        .WithTags("Workflows")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // Delete a state — Admin only. 409 if any issue uses it.
        app.MapDelete("/api/workflows/{workflowId:guid}/states/{stateId:guid}", async (
            Guid workflowId,
            Guid stateId,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IWorkflowQueries workflows,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            var wf = await db.Set<Workflow>().FirstOrDefaultAsync(w => w.Id == workflowId, ct);
            if (wf is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, wf.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;
            if (await workflows.HasIssueInStateAsync(stateId, ct))
                return Results.Conflict(new { error = "Cannot delete a state that has issues. Move the issues first." });
            try { wf.RemoveState(stateId); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "workflow.state.deleted",
                ResourceType: "Project",
                ResourceId: wf.ProjectId.ToString(),
                Summary: $"Deleted workflow state {stateId}",
                Detail: new { workflowId = wf.Id, stateId }), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeleteWorkflowState")
        .WithSummary("Delete a workflow state (Admin only; 409 if any issue uses it)")
        .WithTags("Workflows")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        // Reorder states — Admin only. Body must list every current state id.
        app.MapPut("/api/workflows/{workflowId:guid}/states/reorder", async (
            Guid workflowId,
            [FromBody] ReorderRequest req,
            [FromServices] DbContext db,
            [FromServices] IProjectQueries projects,
            [FromServices] IAuditLog audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            var wf = await db.Set<Workflow>().FirstOrDefaultAsync(w => w.Id == workflowId, ct);
            if (wf is null) return Results.NotFound();
            var auth = await ProjectAccess.RequireAsync(http, projects, wf.ProjectId, ProjectRole.Admin, ct);
            if (auth.Denied) return auth.Failure!;
            try { wf.ReorderStates(req.StateIds); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "workflow.state.reordered",
                ResourceType: "Project",
                ResourceId: wf.ProjectId.ToString(),
                Summary: $"Reordered workflow states",
                Detail: new { workflowId = wf.Id, stateIds = req.StateIds }), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ReorderWorkflowStates")
        .WithSummary("Apply a new state ordering to a workflow (Admin only)")
        .WithTags("Workflows")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
