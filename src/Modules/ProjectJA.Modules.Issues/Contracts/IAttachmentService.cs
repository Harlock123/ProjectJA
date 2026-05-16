// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

public sealed record AttachmentSummary(
    Guid Id,
    Guid IssueId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedBy,
    DateTimeOffset UploadedAt);

public sealed record BeginUploadResult(
    Guid AttachmentId,
    Uri UploadUrl,
    DateTimeOffset ExpiresAt,
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record CompleteUploadRequest(
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes);

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentSummary>> ListAsync(Guid issueId, CancellationToken ct);

    /// <summary>Server-side upload — streams content through the app. Used by API clients
    /// that don't run JS. Browser-side flows should use the Begin/Complete pair below.</summary>
    Task<AttachmentSummary> UploadAsync(
        Guid issueId, string fileName, string contentType, Stream content, long sizeBytes,
        Guid uploadedBy, CancellationToken ct);

    /// <summary>Phase 1 of direct-from-browser upload. Generates a pre-signed PUT URL keyed
    /// to a fresh attachment id; no metadata row written yet.</summary>
    Task<BeginUploadResult?> BeginUploadAsync(
        Guid issueId, string fileName, string contentType, long sizeBytes, CancellationToken ct);

    /// <summary>Phase 2 of direct-from-browser upload. Validates that the storage key matches
    /// the current tenant + attachment id, then persists metadata.</summary>
    Task<AttachmentSummary?> CompleteUploadAsync(
        Guid issueId, Guid attachmentId, CompleteUploadRequest req, Guid uploadedBy, CancellationToken ct);

    Task<Uri?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan ttl, CancellationToken ct);
    Task<bool> DeleteAsync(Guid attachmentId, CancellationToken ct);
}
