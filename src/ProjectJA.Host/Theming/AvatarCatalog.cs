// SPDX-License-Identifier: BUSL-1.1
using MudBlazor;

namespace ProjectJA.Host.Theming;

/// <summary>One selectable avatar preset. The DB stores only the Key; Icon
/// (a Material SVG path) and Color (hex) are looked up here at render time.</summary>
public sealed record AvatarOption(string Key, string Label, string Icon, string Color);

/// <summary>
/// The fixed set of avatars a user can pick from in /settings. Material icons
/// in colored circles — zero new assets, theme-friendly, consistent with the
/// rest of the app. Tweak the list freely; existing users keep their key
/// even if we reorder, so order is purely presentational.
/// </summary>
public sealed class AvatarCatalog
{
    public IReadOnlyList<AvatarOption> Options { get; } =
    [
        new("pets",       "Paw",        Icons.Material.Filled.Pets,           "#8E63E5"),
        new("forest",     "Forest",     Icons.Material.Filled.Forest,         "#2E7D32"),
        new("rocket",     "Rocket",     Icons.Material.Filled.Rocket,         "#E64A19"),
        new("coffee",     "Coffee",     Icons.Material.Filled.LocalCafe,      "#6D4C41"),
        new("headphones", "Headphones", Icons.Material.Filled.Headphones,     "#1565C0"),
        new("bike",       "Bike",       Icons.Material.Filled.PedalBike,      "#00838F"),
        new("brush",      "Brush",      Icons.Material.Filled.Brush,          "#C2185B"),
        new("lightbulb",  "Idea",       Icons.Material.Filled.Lightbulb,      "#F9A825"),
        new("music",      "Music",      Icons.Material.Filled.MusicNote,      "#6A1B9A"),
        new("camera",     "Camera",     Icons.Material.Filled.PhotoCamera,    "#455A64"),
        new("cake",       "Cake",       Icons.Material.Filled.Cake,           "#D81B60"),
        new("stars",      "Stars",      Icons.Material.Filled.Stars,          "#0277BD"),
        new("hiking",     "Hiking",     Icons.Material.Filled.Hiking,         "#5D4037"),
        new("flower",     "Flower",     Icons.Material.Filled.LocalFlorist,   "#AD1457"),
        new("sailing",    "Sailing",    Icons.Material.Filled.Sailing,        "#0097A7"),
        new("diamond",    "Diamond",    Icons.Material.Filled.Diamond,        "#00897B"),
    ];

    public AvatarOption? FindByKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var o in Options)
            if (string.Equals(o.Key, key, StringComparison.Ordinal))
                return o;
        return null;
    }
}
