// SPDX-License-Identifier: BUSL-1.1
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ProjectJA.SharedKernel.Email;

namespace ProjectJA.Infrastructure.Email;

internal sealed class SmtpEmailDispatcher(IOptions<EmailOptions> options) : IEmailDispatcher
{
    public async Task DispatchAsync(EmailMessage message, CancellationToken ct)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.Smtp.Host))
            throw new InvalidOperationException("Email:Smtp:Host is not configured.");

        var mime = new MimeMessage();
        mime.From.Add(ResolveFrom(message, cfg));
        foreach (var to in message.To)
            mime.To.Add(new MailboxAddress(to.DisplayName ?? to.Address, to.Address));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody ?? string.Empty,
        };

        if (message.Attachments is not null)
        {
            foreach (var att in message.Attachments)
            {
                var content = ContentType.Parse(att.ContentType);
                builder.Attachments.Add(att.FileName, att.Content.ToArray(), content);
            }
        }

        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var secureOption = cfg.Smtp.EnableSsl
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.None;
        await client.ConnectAsync(cfg.Smtp.Host, cfg.Smtp.Port, secureOption, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(cfg.Smtp.Username))
            await client.AuthenticateAsync(cfg.Smtp.Username, cfg.Smtp.Password ?? string.Empty, ct).ConfigureAwait(false);
        await client.SendAsync(mime, ct).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);
    }

    private static MailboxAddress ResolveFrom(EmailMessage message, EmailOptions cfg)
    {
        // Caller-supplied From wins; otherwise fall back to configured.
        if (!string.IsNullOrWhiteSpace(message.From.Address))
            return new MailboxAddress(message.From.DisplayName ?? cfg.FromName ?? string.Empty, message.From.Address);
        return new MailboxAddress(cfg.FromName ?? string.Empty, cfg.From);
    }
}
