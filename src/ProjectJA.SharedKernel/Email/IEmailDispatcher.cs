// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Email;

/// <summary>
/// Last-mile email transport (SMTP, Postmark, logging stub). Distinct from
/// <see cref="IEmailSender"/> so callers go through the outbox queue while the
/// dispatch job uses this to actually transmit.
/// </summary>
public interface IEmailDispatcher
{
    Task DispatchAsync(EmailMessage message, CancellationToken ct);
}
