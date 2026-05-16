// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Email;

public sealed record EmailAddress(string Address, string? DisplayName = null);

public sealed record EmailAttachment(string FileName, string ContentType, ReadOnlyMemory<byte> Content);

public sealed record EmailMessage(
    EmailAddress From,
    IReadOnlyList<EmailAddress> To,
    string Subject,
    string HtmlBody,
    string? TextBody = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);
