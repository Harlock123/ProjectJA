// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Contracts;

/// <summary>Server-side per-action authorization for the REST API. Resolves the
/// caller from the auth cookie's NameIdentifier claim, looks up their role in
/// the project, and short-circuits with 401 (not signed in) or 403 (signed in
/// but lacks the role / not a member) when the minimum isn't met. This is the
/// API counterpart of the Blazor pages' role checks — both must enforce; the
/// UI only hides controls.</summary>
public static class ProjectAccess
{
    public readonly record struct Result(IResult? Failure, Guid UserId, ProjectRole Role)
    {
        public bool Denied => Failure is not null;
    }

    public static async Task<Result> RequireAsync(
        HttpContext http, IProjectQueries projects, Guid projectId,
        ProjectRole minimum, CancellationToken ct)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId))
            return new Result(Results.Unauthorized(), Guid.Empty, default);

        var role = await projects.GetRoleAsync(projectId, userId, ct);
        if (role is null || role.Value < minimum)
            return new Result(
                Results.Json(
                    new { error = "You don't have permission to do this in this project." },
                    statusCode: StatusCodes.Status403Forbidden),
                userId,
                role ?? default);

        return new Result(null, userId, role.Value);
    }
}
