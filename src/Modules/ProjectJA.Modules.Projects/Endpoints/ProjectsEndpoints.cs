// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Projects.Endpoints;

internal static class ProjectsEndpoints
{
    internal sealed record CreateProjectRequest(string Key, string Name, string? Description);
    internal sealed record ProjectDto(Guid Id, string Key, string Name, string? Description, DateTimeOffset CreatedAt);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");

        group.MapGet("/", async ([FromServices] DbContext db, CancellationToken ct) =>
        {
            var items = await db.Set<Project>()
                .AsNoTracking()
                .OrderBy(p => p.Key)
                .Select(p => new ProjectDto(p.Id, p.Key, p.Name, p.Description, p.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(items);
        })
        .WithName("ListProjects")
        .WithSummary("List all projects in the current tenant")
        .Produces<List<ProjectDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, [FromServices] DbContext db, CancellationToken ct) =>
        {
            var p = await db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return p is null
                ? Results.NotFound()
                : Results.Ok(new ProjectDto(p.Id, p.Key, p.Name, p.Description, p.CreatedAt));
        })
        .WithName("GetProject")
        .WithSummary("Get a single project by id")
        .Produces<ProjectDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            [FromBody] CreateProjectRequest req,
            [FromServices] DbContext db,
            [FromServices] IOrganizationQueries orgs,
            [FromServices] IAuditLog audit,
            [FromServices] IClock clock,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Key and Name are required." });

            var createdByClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(createdByClaim, out var createdBy))
                return Results.Unauthorized();

            var org = await orgs.GetDefaultAsync(ct);
            if (org is null)
                return Results.BadRequest(new { error = "No organization exists. Provision one first." });

            var project = Project.Create(org.Id, req.Key, req.Name, req.Description, createdBy, clock.UtcNow);
            db.Set<Project>().Add(project);
            await db.SaveChangesAsync(ct);

            await audit.RecordAsync(new AuditEntry(
                Action: "project.created",
                ResourceType: "Project",
                ResourceId: project.Id.ToString(),
                Summary: $"Project {project.Key} created",
                Detail: new { project.Key, project.Name }), ct);

            return Results.Created($"/api/projects/{project.Id}",
                new ProjectDto(project.Id, project.Key, project.Name, project.Description, project.CreatedAt));
        })
        .RequireAuthorization()
        .WithName("CreateProject")
        .WithSummary("Create a new project")
        .Produces<ProjectDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem();

        return app;
    }
}
