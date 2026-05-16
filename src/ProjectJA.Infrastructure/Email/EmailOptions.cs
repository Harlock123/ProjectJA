// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Infrastructure.Email;

public sealed class EmailOptions
{
    /// <summary>"smtp", "postmark", or "logging" (default).</summary>
    public string Driver { get; set; } = "logging";

    /// <summary>From-address used when EmailMessage.From is the placeholder default.</summary>
    public string From { get; set; } = "noreply@projectja.local";
    public string? FromName { get; set; } = "ProjectJA";

    public SmtpOptions Smtp { get; set; } = new();
    public PostmarkOptions Postmark { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class PostmarkOptions
{
    public string? ServerToken { get; set; }
    public string MessageStream { get; set; } = "outbound";
}
