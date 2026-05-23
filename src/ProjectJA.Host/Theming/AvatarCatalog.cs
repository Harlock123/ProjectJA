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
    // Existing keys preserved verbatim — users already pointing at e.g. "pets"
    // must keep their selection valid across catalog expansions.
    public IReadOnlyList<AvatarOption> Options { get; } =
    [
        // ---- Animals / nature ----
        new("pets",        "Paw",          Icons.Material.Filled.Pets,             "#8E63E5"),
        new("rabbit",      "Rabbit",       Icons.Material.Filled.CrueltyFree,      "#EC407A"),
        new("bee",         "Bee",          Icons.Material.Filled.EmojiNature,      "#FFA000"),
        new("forest",      "Forest",       Icons.Material.Filled.Forest,           "#2E7D32"),
        new("park",        "Park",         Icons.Material.Filled.Park,             "#33691E"),
        new("flower",      "Flower",       Icons.Material.Filled.LocalFlorist,     "#AD1457"),
        new("spa",         "Leaf",         Icons.Material.Filled.Spa,              "#689F38"),
        new("mountain",    "Mountain",     Icons.Material.Filled.Terrain,          "#4E342E"),

        // ---- Weather / sky ----
        new("sun",         "Sun",          Icons.Material.Filled.WbSunny,          "#FB8C00"),
        new("cloud",       "Cloud",        Icons.Material.Filled.Cloud,            "#78909C"),
        new("water",       "Water",        Icons.Material.Filled.WaterDrop,        "#1E88E5"),
        new("snowflake",   "Snowflake",    Icons.Material.Filled.AcUnit,           "#4DD0E1"),
        new("globe",       "Globe",        Icons.Material.Filled.Public,           "#1976D2"),

        // ---- Travel / vehicles ----
        new("rocket",      "Rocket",       Icons.Material.Filled.Rocket,           "#E64A19"),
        new("flight",      "Flight",       Icons.Material.Filled.Flight,           "#039BE5"),
        new("car",         "Car",          Icons.Material.Filled.DirectionsCar,    "#1A237E"),
        new("bike",        "Bike",         Icons.Material.Filled.PedalBike,        "#00838F"),
        new("scooter",     "Scooter",      Icons.Material.Filled.TwoWheeler,       "#D32F2F"),
        new("train",       "Train",        Icons.Material.Filled.Train,            "#546E7A"),
        new("sailing",     "Sailing",      Icons.Material.Filled.Sailing,          "#0097A7"),
        new("hiking",      "Hiking",       Icons.Material.Filled.Hiking,           "#5D4037"),
        new("snowboard",   "Snowboard",    Icons.Material.Filled.Snowboarding,     "#283593"),
        new("surf",        "Surf",         Icons.Material.Filled.Surfing,          "#00695C"),

        // ---- Food / drink ----
        new("coffee",      "Coffee",       Icons.Material.Filled.LocalCafe,        "#6D4C41"),
        new("cake",        "Cake",         Icons.Material.Filled.Cake,             "#D81B60"),
        new("icecream",    "Ice cream",    Icons.Material.Filled.Icecream,         "#F48FB1"),
        new("pizza",       "Pizza",        Icons.Material.Filled.LocalPizza,       "#E53935"),
        new("burger",      "Burger",       Icons.Material.Filled.LunchDining,      "#EF6C00"),
        new("ramen",       "Ramen",        Icons.Material.Filled.RamenDining,      "#C62828"),
        new("cocktail",    "Cocktail",     Icons.Material.Filled.LocalBar,         "#FF8F00"),
        new("wine",        "Wine",         Icons.Material.Filled.WineBar,          "#4A148C"),

        // ---- Hobbies / creativity ----
        new("headphones",  "Headphones",   Icons.Material.Filled.Headphones,       "#1565C0"),
        new("music",       "Music",        Icons.Material.Filled.MusicNote,        "#6A1B9A"),
        new("piano",       "Piano",        Icons.Material.Filled.Piano,            "#212121"),
        new("brush",       "Brush",        Icons.Material.Filled.Brush,            "#C2185B"),
        new("palette",     "Palette",      Icons.Material.Filled.Palette,          "#F4511E"),
        new("camera",      "Camera",       Icons.Material.Filled.PhotoCamera,      "#455A64"),
        new("gamepad",     "Gamepad",      Icons.Material.Filled.SportsEsports,    "#B71C1C"),
        new("basketball",  "Basketball",   Icons.Material.Filled.SportsBasketball, "#E65100"),

        // ---- Symbols / abstract ----
        new("lightbulb",   "Idea",         Icons.Material.Filled.Lightbulb,        "#F9A825"),
        new("stars",       "Stars",        Icons.Material.Filled.Stars,            "#0277BD"),
        new("diamond",     "Diamond",      Icons.Material.Filled.Diamond,          "#00897B"),
        new("heart",       "Heart",        Icons.Material.Filled.Favorite,         "#E91E63"),
        new("hammer",      "Hammer",       Icons.Material.Filled.Handyman,         "#795548"),
        new("flask",       "Flask",        Icons.Material.Filled.Science,          "#00796B"),
        new("brain",       "Brain",        Icons.Material.Filled.Psychology,       "#9C27B0"),
        new("book",        "Book",         Icons.Material.Filled.AutoStories,      "#3949AB"),
        new("code",        "Code",         Icons.Material.Filled.Code,             "#006064"),
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
