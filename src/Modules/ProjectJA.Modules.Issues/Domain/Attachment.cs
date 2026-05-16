// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

public sealed class Attachment
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    private Attachment() { }

    public Attachment(
        Guid id,
        Guid issueId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid uploadedBy,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        IssueId = issueId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
    }

    public static Attachment Create(
        Guid id, Guid issueId, string fileName, string contentType, long sizeBytes,
        string storageKey, Guid uploadedBy, DateTimeOffset now)
        => new(id, issueId, fileName, contentType, sizeBytes, storageKey, uploadedBy, now);
}
