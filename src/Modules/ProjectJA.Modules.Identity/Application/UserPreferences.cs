// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Application;

internal sealed class UserPreferences(DbContext db) : IUserPreferences
{
    public async Task<ThemePreference> GetThemeAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new ThemePreference(u.ThemeKey, u.ThemeDark))
            .FirstOrDefaultAsync(ct);

        return row ?? new ThemePreference(null, false);
    }

    public async Task SetThemeAsync(Guid userId, string? themeKey, bool dark, CancellationToken ct)
    {
        var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        user.ThemeKey = themeKey;
        user.ThemeDark = dark;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAvatarKeyAsync(Guid userId, CancellationToken ct)
    {
        return await db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.AvatarKey)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SetAvatarKeyAsync(Guid userId, string? avatarKey, CancellationToken ct)
    {
        var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;
        user.AvatarKey = avatarKey;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> GetTooltipsEnabledAsync(Guid userId, CancellationToken ct)
    {
        // Default true if the row exists but the value is null-ish from a
        // legacy seeded row — the column default is also true at the DB level
        // so this is just defense-in-depth.
        var row = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (bool?)u.TooltipsEnabled)
            .FirstOrDefaultAsync(ct);
        return row ?? true;
    }

    public async Task SetTooltipsEnabledAsync(Guid userId, bool enabled, CancellationToken ct)
    {
        var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;
        user.TooltipsEnabled = enabled;
        await db.SaveChangesAsync(ct);
    }
}
