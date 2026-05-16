// SPDX-License-Identifier: BUSL-1.1
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using ProjectJA.SharedKernel.Email;

namespace ProjectJA.Infrastructure.Email;

internal sealed class PostmarkEmailDispatcher(HttpClient http, IOptions<EmailOptions> options) : IEmailDispatcher
{
    private const string Endpoint = "https://api.postmarkapp.com/email";

    public async Task DispatchAsync(EmailMessage message, CancellationToken ct)
    {
        var cfg = options.Value;
        if (string.IsNullOrWhiteSpace(cfg.Postmark.ServerToken))
            throw new InvalidOperationException("Email:Postmark:ServerToken is not configured.");

        var fromAddress = string.IsNullOrWhiteSpace(message.From.Address) ? cfg.From : message.From.Address;
        var fromName = message.From.DisplayName ?? cfg.FromName;
        var from = string.IsNullOrEmpty(fromName) ? fromAddress : $"\"{fromName}\" <{fromAddress}>";

        var payload = new PostmarkPayload(
            From: from,
            To: string.Join(",", message.To.Select(t => t.Address)),
            Subject: message.Subject,
            HtmlBody: message.HtmlBody,
            TextBody: message.TextBody,
            MessageStream: cfg.Postmark.MessageStream,
            Attachments: message.Attachments?
                .Select(a => new PostmarkAttachment(
                    a.FileName,
                    Convert.ToBase64String(a.Content.Span),
                    a.ContentType))
                .ToList());

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Postmark-Server-Token", cfg.Postmark.ServerToken);

        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Postmark send failed ({(int)response.StatusCode}): {body}");
        }
    }

    private sealed record PostmarkPayload(
        string From,
        string To,
        string Subject,
        string HtmlBody,
        string? TextBody,
        string MessageStream,
        IReadOnlyList<PostmarkAttachment>? Attachments);

    private sealed record PostmarkAttachment(string Name, string Content, string ContentType);
}
