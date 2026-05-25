// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Contracts;

/// <summary>A user's UI appearance preference. ThemeKey is null when the user
/// has never chosen one (the app default applies).</summary>
public sealed record ThemePreference(string? ThemeKey, bool Dark);

public interface IUserPreferences
{
    Task<ThemePreference> GetThemeAsync(Guid userId, CancellationToken ct);
    Task SetThemeAsync(Guid userId, string? themeKey, bool dark, CancellationToken ct);
    Task<string?> GetAvatarKeyAsync(Guid userId, CancellationToken ct);
    Task SetAvatarKeyAsync(Guid userId, string? avatarKey, CancellationToken ct);
    Task<bool> GetTooltipsEnabledAsync(Guid userId, CancellationToken ct);
    Task SetTooltipsEnabledAsync(Guid userId, bool enabled, CancellationToken ct);
}
