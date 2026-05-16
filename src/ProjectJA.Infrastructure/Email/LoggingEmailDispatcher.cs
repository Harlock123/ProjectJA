// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.Logging;
using ProjectJA.SharedKernel.Email;

namespace ProjectJA.Infrastructure.Email;

/// <summary>
/// Stub dispatcher that writes the message to the log instead of transmitting it.
/// Used as the default when no driver is configured.
/// </summary>
internal sealed class LoggingEmailDispatcher(ILogger<LoggingEmailDispatcher> logger) : IEmailDispatcher
{
    public Task DispatchAsync(EmailMessage message, CancellationToken ct)
    {
        var to = string.Join(", ", message.To.Select(t => t.Address));
        logger.LogInformation(
            "[email-stub] to={To} subject={Subject}\n{Body}",
            to, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}
