// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Theming;

/// <summary>
/// Per-circuit (scoped) holder of the active theme selection. MainLayout
/// renders from it and subscribes to <see cref="Changed"/> so a change made
/// on the settings page applies live without a page reload.
/// </summary>
public sealed class ThemeState
{
    public string? ThemeKey { get; private set; }
    public bool IsDark { get; private set; }

    public event Action? Changed;

    public void Set(string? themeKey, bool isDark)
    {
        ThemeKey = themeKey;
        IsDark = isDark;
        Changed?.Invoke();
    }
}
