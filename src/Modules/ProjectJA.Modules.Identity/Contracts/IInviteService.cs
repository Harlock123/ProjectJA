// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Contracts;

public sealed record InviteSummary(
    Guid Id,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpired);

public sealed record CreatedInvite(Guid Id, string Email, string AcceptUrl, DateTimeOffset ExpiresAt);

public sealed record AcceptInviteRequest(string Token, string Password, string FirstName, string LastName);

public sealed record AcceptInviteResult(bool Success, string? Error, Guid? UserId, string? Email);

public interface IInviteService
{
    Task<CreatedInvite> CreateAsync(string email, Guid createdById, string baseUrl, CancellationToken ct);
    Task<IReadOnlyList<InviteSummary>> ListPendingAsync(CancellationToken ct);
    Task<bool> RevokeAsync(Guid inviteId, CancellationToken ct);
    Task<AcceptInviteResult> AcceptAsync(AcceptInviteRequest request, CancellationToken ct);
}
