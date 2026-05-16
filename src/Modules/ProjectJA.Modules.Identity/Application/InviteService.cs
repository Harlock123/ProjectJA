// SPDX-License-Identifier: BUSL-1.1
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Email;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Identity.Application;

internal sealed class InviteService(
    DbContext db,
    UserManager<ApplicationUser> users,
    IOrganizationQueries orgs,
    IEmailSender email,
    IAuditLog audit,
    IClock clock) : IInviteService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);

    public async Task<CreatedInvite> CreateAsync(string emailAddress, Guid createdById, string baseUrl, CancellationToken ct)
    {
        var org = await orgs.GetDefaultAsync(ct)
            ?? throw new InvalidOperationException("Tenant has no organization.");

        var rawToken = GenerateToken();
        var hash = HashToken(rawToken);

        var invite = Invite.Create(
            organizationId: org.Id,
            email: emailAddress,
            tokenHash: hash,
            createdById: createdById,
            now: clock.UtcNow,
            ttl: DefaultTtl);

        db.Set<Invite>().Add(invite);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "invite.created",
            ResourceType: "Invite",
            ResourceId: invite.Id.ToString(),
            Summary: $"Invite sent to {invite.Email}",
            Detail: new { invite.Email, invite.ExpiresAt }), ct);

        var trimmedBase = baseUrl.TrimEnd('/');
        var acceptUrl = $"{trimmedBase}/accept-invite?token={rawToken}";

        // Best-effort email — implementation may be the logging fallback.
        try
        {
            var message = new EmailMessage(
                From: new EmailAddress("noreply@projectja.local", "ProjectJA"),
                To: new[] { new EmailAddress(invite.Email) },
                Subject: $"You're invited to {org.Name}",
                HtmlBody:
                    $"<p>You've been invited to join <strong>{System.Net.WebUtility.HtmlEncode(org.Name)}</strong> on ProjectJA.</p>" +
                    $"<p><a href=\"{acceptUrl}\">Accept your invite</a></p>" +
                    $"<p>This link expires on {invite.ExpiresAt:u}.</p>");
            await email.SendAsync(message, ct);
        }
        catch
        {
            // Email failures don't fail the invite — the admin can copy the link from the UI.
        }

        return new CreatedInvite(invite.Id, invite.Email, acceptUrl, invite.ExpiresAt);
    }

    public async Task<IReadOnlyList<InviteSummary>> ListPendingAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var rows = await db.Set<Invite>()
            .AsNoTracking()
            .Where(i => i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InviteSummary(i.Id, i.Email, i.CreatedAt, i.ExpiresAt, i.ExpiresAt <= now))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<bool> RevokeAsync(Guid inviteId, CancellationToken ct)
    {
        var invite = await db.Set<Invite>().FirstOrDefaultAsync(i => i.Id == inviteId, ct);
        if (invite is null || invite.AcceptedAt is not null) return false;
        db.Set<Invite>().Remove(invite);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "invite.revoked",
            ResourceType: "Invite",
            ResourceId: invite.Id.ToString(),
            Summary: $"Invite to {invite.Email} revoked"), ct);

        return true;
    }

    public async Task<AcceptInviteResult> AcceptAsync(AcceptInviteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return new AcceptInviteResult(false, "Token is required.", null, null);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
            return new AcceptInviteResult(false, "Password must be at least 8 characters.", null, null);

        var hash = HashToken(request.Token);
        var invite = await db.Set<Invite>().FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invite is null || !invite.IsActive(clock.UtcNow))
            return new AcceptInviteResult(false, "Invite is invalid or expired.", null, null);

        var existing = await users.FindByEmailAsync(invite.Email);
        if (existing is not null)
            return new AcceptInviteResult(false, "An account with this email already exists.", null, null);

        var user = new ApplicationUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            EmailConfirmed = true,
            FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? "" : request.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? "" : request.LastName.Trim(),
            OrganizationId = invite.OrganizationId,
            CreatedAt = clock.UtcNow,
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return new AcceptInviteResult(false, string.Join("; ", result.Errors.Select(e => e.Description)), null, null);

        invite.MarkAccepted(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "invite.accepted",
            ResourceType: "Invite",
            ResourceId: invite.Id.ToString(),
            Summary: $"Invite accepted by {invite.Email}",
            Detail: new { newUserId = user.Id }), ct);

        return new AcceptInviteResult(true, null, user.Id, user.Email);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
