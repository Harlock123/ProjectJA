// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Theming;

/// <summary>
/// Per-circuit (scoped) holder of the signed-in user's "show helper
/// tooltips" preference. The <see cref="HelpTooltip"/> component reads
/// <see cref="Enabled"/> on every render so toggling from Settings reflects
/// across the open page without a reload. Same pattern as
/// <c>ThemeState</c> and <c>AvatarState</c>.
/// </summary>
public sealed class TooltipState
{
    /// <summary>True = show informational tooltips on hover. Default true so
    /// new users see them until they explicitly opt out.</summary>
    public bool Enabled { get; private set; } = true;

    public event Action? Changed;

    public void Set(bool enabled)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        Changed?.Invoke();
    }
}
