// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Audit.Application;

public sealed class AuditOptions
{
    /// <summary>How many days of audit history to retain. Older events get purged daily.</summary>
    public int RetentionDays { get; set; } = 90;
}
