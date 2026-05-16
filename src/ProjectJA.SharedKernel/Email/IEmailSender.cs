// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
