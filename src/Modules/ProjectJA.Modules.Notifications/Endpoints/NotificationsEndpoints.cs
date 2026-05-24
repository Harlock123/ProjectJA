// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ProjectJA.SharedKernel.Notifications;

namespace ProjectJA.Modules.Notifications.Endpoints;

internal static class NotificationsEndpoints
{
    internal static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] int? limit,
            [FromServices] INotificationService notifications,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId)) return Results.Unauthorized();
            return Results.Ok(await notifications.ListForUserAsync(userId, limit ?? 20, ct));
        })
        .WithName("ListNotifications")
        .WithSummary("List the signed-in user's recent notifications (newest first).")
        .WithTags("Notifications")
        .Produces<IReadOnlyList<NotificationView>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/unread-count", async (
            [FromServices] INotificationService notifications,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId)) return Results.Unauthorized();
            var count = await notifications.GetUnreadCountAsync(userId, ct);
            return Results.Ok(new { count });
        })
        .WithName("UnreadNotificationCount")
        .WithSummary("Count of unread notifications for the signed-in user.")
        .WithTags("Notifications")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/{id:guid}/read", async (
            Guid id,
            [FromServices] INotificationService notifications,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId)) return Results.Unauthorized();
            await notifications.MarkReadAsync(id, userId, ct);
            return Results.NoContent();
        })
        .WithName("MarkNotificationRead")
        .WithSummary("Mark a single notification as read.")
        .WithTags("Notifications")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/read-all", async (
            [FromServices] INotificationService notifications,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId)) return Results.Unauthorized();
            await notifications.MarkAllReadAsync(userId, ct);
            return Results.NoContent();
        })
        .WithName("MarkAllNotificationsRead")
        .WithSummary("Mark every unread notification as read for the signed-in user.")
        .WithTags("Notifications")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static bool TryGetUserId(HttpContext http, out Guid userId)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }
}
