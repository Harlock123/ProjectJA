// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Storage;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class AttachmentService(
    DbContext db,
    IObjectStore storage,
    ITenantContext tenant,
    IAuditLog audit,
    IClock clock) : IAttachmentService
{
    public async Task<IReadOnlyList<AttachmentSummary>> ListAsync(Guid issueId, CancellationToken ct)
    {
        return await db.Set<Attachment>()
            .AsNoTracking()
            .Where(a => a.IssueId == issueId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new AttachmentSummary(
                a.Id, a.IssueId, a.FileName, a.ContentType, a.SizeBytes, a.UploadedBy, a.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<AttachmentSummary> UploadAsync(
        Guid issueId, string fileName, string contentType, Stream content, long sizeBytes,
        Guid uploadedBy, CancellationToken ct)
    {
        var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null)
            throw new InvalidOperationException($"Issue {issueId} not found.");

        var attachmentId = Guid.NewGuid();
        var storageKey = BuildKey(tenant.Current.Value, attachmentId, fileName);
        await storage.PutAsync(storageKey, content, contentType, ct);

        var attachment = Attachment.Create(
            attachmentId, issueId, fileName, contentType, sizeBytes, storageKey, uploadedBy, clock.UtcNow);
        db.Set<Attachment>().Add(attachment);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "attachment.created",
            ResourceType: "Attachment",
            ResourceId: attachment.Id.ToString(),
            Summary: $"Attached '{fileName}' to issue {issueId}",
            Detail: new { fileName, contentType, sizeBytes }), ct);

        return new AttachmentSummary(
            attachment.Id, attachment.IssueId, attachment.FileName, attachment.ContentType,
            attachment.SizeBytes, attachment.UploadedBy, attachment.UploadedAt);
    }

    public async Task<BeginUploadResult?> BeginUploadAsync(
        Guid issueId, string fileName, string contentType, long sizeBytes, CancellationToken ct)
    {
        var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return null;

        var attachmentId = Guid.NewGuid();
        var ctype = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        var storageKey = BuildKey(tenant.Current.Value, attachmentId, fileName);
        var ttl = TimeSpan.FromMinutes(15);
        var presigned = await storage.CreateUploadUrlAsync(storageKey, ctype, ttl, ct);

        return new BeginUploadResult(
            attachmentId, presigned.Url, presigned.ExpiresAt, storageKey, fileName, ctype, sizeBytes);
    }

    public async Task<AttachmentSummary?> CompleteUploadAsync(
        Guid issueId, Guid attachmentId, CompleteUploadRequest req, Guid uploadedBy, CancellationToken ct)
    {
        var issue = await db.Set<Issue>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return null;

        // Reject any storage key the client made up: it must match what BeginUploadAsync
        // would have generated for this tenant + attachment id.
        var expectedKey = BuildKey(tenant.Current.Value, attachmentId, req.FileName);
        if (!string.Equals(req.StorageKey, expectedKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage key does not match the expected tenant/attachment prefix.");

        var attachment = Attachment.Create(
            attachmentId, issueId, req.FileName, req.ContentType, req.SizeBytes,
            req.StorageKey, uploadedBy, clock.UtcNow);
        db.Set<Attachment>().Add(attachment);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "attachment.created",
            ResourceType: "Attachment",
            ResourceId: attachment.Id.ToString(),
            Summary: $"Attached '{req.FileName}' to issue {issueId}",
            Detail: new { req.FileName, req.ContentType, req.SizeBytes, source = "presigned" }), ct);

        return new AttachmentSummary(
            attachment.Id, attachment.IssueId, attachment.FileName, attachment.ContentType,
            attachment.SizeBytes, attachment.UploadedBy, attachment.UploadedAt);
    }

    public async Task<Uri?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan ttl, CancellationToken ct)
    {
        var attachment = await db.Set<Attachment>().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment is null) return null;
        var url = await storage.CreateDownloadUrlAsync(attachment.StorageKey, ttl, ct);
        return url.Url;
    }

    public async Task<bool> DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (attachment is null) return false;

        try
        {
            await storage.DeleteAsync(attachment.StorageKey, ct);
        }
        catch
        {
            // Best-effort: even if the object delete fails (already gone, transient error),
            // we still remove the metadata so the user sees the attachment as deleted.
        }

        db.Set<Attachment>().Remove(attachment);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "attachment.deleted",
            ResourceType: "Attachment",
            ResourceId: attachment.Id.ToString(),
            Summary: $"Removed attachment '{attachment.FileName}'"), ct);

        return true;
    }

    private static string BuildKey(Guid tenantId, Guid attachmentId, string fileName)
    {
        var safeName = string.Concat(Path.GetFileName(fileName)
            .Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_'));
        return $"tenants/{tenantId:N}/attachments/{attachmentId:N}/{safeName}";
    }
}
