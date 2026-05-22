// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Theming;

/// <summary>
/// Per-circuit (scoped) holder of the signed-in user's avatar selection.
/// MainLayout renders from it and subscribes to <see cref="Changed"/> so a
/// pick on the settings page reflects in the top-right without a reload.
/// </summary>
public sealed class AvatarState
{
    public string? AvatarKey { get; private set; }

    public event Action? Changed;

    public void Set(string? avatarKey)
    {
        AvatarKey = avatarKey;
        Changed?.Invoke();
    }
}
