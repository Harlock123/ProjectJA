// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ProjectJA.Modules.Identity.Contracts;

namespace ProjectJA.Modules.Identity.Endpoints;

internal static class IdentityEndpoints
{
    internal sealed record CreateInviteRequest(string Email);

    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        // Invites are an org-level concern — creating one makes a new tenant
        // user — so gate the whole group behind the OrgAdmin policy.
        var group = app.MapGroup("/api/invites").RequireAuthorization("OrgAdmin");

        group.MapGet("/", async ([FromServices] IInviteService invites, CancellationToken ct) =>
        {
            var pending = await invites.ListPendingAsync(ct);
            return Results.Ok(pending);
        });

        group.MapPost("/", async (
            [FromBody] CreateInviteRequest req,
            [FromServices] IInviteService invites,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "Email is required." });

            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var createdById))
                return Results.Unauthorized();

            var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
            var invite = await invites.CreateAsync(req.Email, createdById, baseUrl, ct);
            return Results.Created($"/api/invites/{invite.Id}", invite);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IInviteService invites,
            CancellationToken ct) =>
        {
            return await invites.RevokeAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
