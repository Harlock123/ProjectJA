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
        new("pets",         "Paw",          Icons.Material.Filled.Pets,             "#8E63E5"),
        new("rabbit",       "Rabbit",       Icons.Material.Filled.CrueltyFree,      "#EC407A"),
        new("bee",          "Bee",          Icons.Material.Filled.EmojiNature,      "#FFA000"),
        new("forest",       "Forest",       Icons.Material.Filled.Forest,           "#2E7D32"),
        new("park",         "Park",         Icons.Material.Filled.Park,             "#33691E"),
        new("flower",       "Flower",       Icons.Material.Filled.LocalFlorist,     "#AD1457"),
        new("spa",          "Leaf",         Icons.Material.Filled.Spa,              "#689F38"),
        new("mountain",     "Mountain",     Icons.Material.Filled.Terrain,          "#4E342E"),

        // ---- Weather / sky ----
        new("sun",          "Sun",          Icons.Material.Filled.WbSunny,          "#FB8C00"),
        new("moon",         "Moon",         Icons.Material.Filled.NightlightRound,  "#303F9F"),
        new("cloud",        "Cloud",        Icons.Material.Filled.Cloud,            "#78909C"),
        new("cloudy",       "Cloudy",       Icons.Material.Filled.WbCloudy,         "#607D8B"),
        new("wind",         "Wind",         Icons.Material.Filled.Air,              "#0288D1"),
        new("water",        "Water",        Icons.Material.Filled.WaterDrop,        "#1E88E5"),
        new("snowflake",    "Snowflake",    Icons.Material.Filled.AcUnit,           "#4DD0E1"),
        new("storm",        "Storm",        Icons.Material.Filled.Storm,            "#37474F"),
        new("umbrella",     "Beach",        Icons.Material.Filled.BeachAccess,      "#00ACC1"),
        new("globe",        "Globe",        Icons.Material.Filled.Public,           "#1976D2"),

        // ---- Travel / vehicles ----
        new("rocket",       "Rocket",       Icons.Material.Filled.Rocket,           "#E64A19"),
        new("flight",       "Flight",       Icons.Material.Filled.Flight,           "#039BE5"),
        new("car",          "Car",          Icons.Material.Filled.DirectionsCar,    "#1A237E"),
        new("bike",         "Bike",         Icons.Material.Filled.PedalBike,        "#00838F"),
        new("scooter",      "Scooter",      Icons.Material.Filled.TwoWheeler,       "#D32F2F"),
        new("train",        "Train",        Icons.Material.Filled.Train,            "#546E7A"),
        new("taxi",         "Taxi",         Icons.Material.Filled.LocalTaxi,        "#FBC02D"),
        new("truck",        "Truck",        Icons.Material.Filled.LocalShipping,    "#3E2723"),
        new("sailing",      "Sailing",      Icons.Material.Filled.Sailing,          "#0097A7"),
        new("boat",         "Boat",         Icons.Material.Filled.DirectionsBoat,   "#006064"),
        new("anchor",       "Anchor",       Icons.Material.Filled.Anchor,           "#0D47A1"),
        new("hiking",       "Hiking",       Icons.Material.Filled.Hiking,           "#5D4037"),
        new("walking",      "Walking",      Icons.Material.Filled.DirectionsWalk,   "#558B2F"),
        new("running",      "Running",      Icons.Material.Filled.DirectionsRun,    "#8E24AA"),
        new("snowboard",    "Snowboard",    Icons.Material.Filled.Snowboarding,     "#283593"),
        new("surf",         "Surf",         Icons.Material.Filled.Surfing,          "#00695C"),
        new("skateboard",   "Skateboard",   Icons.Material.Filled.Skateboarding,    "#F57C00"),

        // ---- Food / drink ----
        new("coffee",       "Coffee",       Icons.Material.Filled.LocalCafe,        "#6D4C41"),
        new("cake",         "Cake",         Icons.Material.Filled.Cake,             "#D81B60"),
        new("icecream",     "Ice cream",    Icons.Material.Filled.Icecream,         "#F48FB1"),
        new("cookie",       "Cookie",       Icons.Material.Filled.Cookie,           "#8D6E63"),
        new("croissant",    "Croissant",    Icons.Material.Filled.BakeryDining,     "#A1887F"),
        new("brunch",       "Brunch",       Icons.Material.Filled.BrunchDining,     "#FFA726"),
        new("pizza",        "Pizza",        Icons.Material.Filled.LocalPizza,       "#E53935"),
        new("burger",       "Burger",       Icons.Material.Filled.LunchDining,      "#EF6C00"),
        new("ramen",        "Ramen",        Icons.Material.Filled.RamenDining,      "#C62828"),
        new("tapas",        "Tapas",        Icons.Material.Filled.Tapas,            "#D84315"),
        new("restaurant",   "Restaurant",   Icons.Material.Filled.Restaurant,       "#BF360C"),
        new("fastfood",     "Fastfood",     Icons.Material.Filled.Fastfood,         "#FF7043"),
        new("cocktail",     "Cocktail",     Icons.Material.Filled.LocalBar,         "#FF8F00"),
        new("wine",         "Wine",         Icons.Material.Filled.WineBar,          "#4A148C"),

        // ---- Hobbies / creativity ----
        new("headphones",   "Headphones",   Icons.Material.Filled.Headphones,       "#1565C0"),
        new("music",        "Music",        Icons.Material.Filled.MusicNote,        "#6A1B9A"),
        new("piano",        "Piano",        Icons.Material.Filled.Piano,            "#212121"),
        new("theater",      "Theater",      Icons.Material.Filled.TheaterComedy,    "#4527A0"),
        new("brush",        "Brush",        Icons.Material.Filled.Brush,            "#C2185B"),
        new("palette",      "Palette",      Icons.Material.Filled.Palette,          "#F4511E"),
        new("camera",       "Camera",       Icons.Material.Filled.PhotoCamera,      "#455A64"),
        new("gamepad",      "Gamepad",      Icons.Material.Filled.SportsEsports,    "#B71C1C"),
        new("basketball",   "Basketball",   Icons.Material.Filled.SportsBasketball, "#E65100"),
        new("baseball",     "Baseball",     Icons.Material.Filled.SportsBaseball,   "#EF5350"),
        new("football",     "Football",     Icons.Material.Filled.SportsFootball,   "#1B5E20"),
        new("golf",         "Golf",         Icons.Material.Filled.SportsGolf,       "#7CB342"),
        new("tennis",       "Tennis",       Icons.Material.Filled.SportsTennis,     "#C0CA33"),
        new("volleyball",   "Volleyball",   Icons.Material.Filled.SportsVolleyball, "#FF6F00"),
        new("dumbbell",     "Dumbbell",     Icons.Material.Filled.FitnessCenter,    "#424242"),
        new("yoga",         "Yoga",         Icons.Material.Filled.SelfImprovement,  "#5E35B1"),
        new("pool",         "Pool",         Icons.Material.Filled.Pool,             "#0091EA"),
        new("rowing",       "Rowing",       Icons.Material.Filled.Rowing,           "#00838F"),

        // ---- Symbols / abstract ----
        new("lightbulb",    "Idea",         Icons.Material.Filled.Lightbulb,        "#F9A825"),
        new("stars",        "Stars",        Icons.Material.Filled.Stars,            "#0277BD"),
        new("sparkles",     "Sparkles",     Icons.Material.Filled.AutoAwesome,      "#AB47BC"),
        new("diamond",      "Diamond",      Icons.Material.Filled.Diamond,          "#00897B"),
        new("heart",        "Heart",        Icons.Material.Filled.Favorite,         "#E91E63"),
        new("hammer",       "Hammer",       Icons.Material.Filled.Handyman,         "#795548"),
        new("flask",        "Flask",        Icons.Material.Filled.Science,          "#00796B"),
        new("brain",        "Brain",        Icons.Material.Filled.Psychology,       "#9C27B0"),
        new("book",         "Book",         Icons.Material.Filled.AutoStories,      "#3949AB"),
        new("code",         "Code",         Icons.Material.Filled.Code,             "#006064"),
        new("flame",        "Flame",        Icons.Material.Filled.Whatshot,         "#FF5722"),
        new("fire",         "Campfire",     Icons.Material.Filled.LocalFireDepartment, "#BF360C"),
        new("lightning",    "Lightning",    Icons.Material.Filled.Bolt,             "#FF6F00"),
        new("trophy",       "Trophy",       Icons.Material.Filled.EmojiEvents,      "#FFD600"),
        new("badge",        "Badge",        Icons.Material.Filled.WorkspacePremium, "#F57F17"),
        new("verified",     "Verified",     Icons.Material.Filled.Verified,         "#00C853"),
        new("gift",         "Gift",         Icons.Material.Filled.Redeem,           "#880E4F"),
        new("ticket",       "Ticket",       Icons.Material.Filled.LocalActivity,    "#6A1B9A"),
        new("handshake",    "Handshake",    Icons.Material.Filled.Handshake,        "#5D4037"),
        new("face",         "Face",         Icons.Material.Filled.Face,             "#FF7043"),
        new("smile",        "Smile",        Icons.Material.Filled.EmojiEmotions,    "#FFB300"),
        new("shield",       "Shield",       Icons.Material.Filled.Security,         "#004D40"),
        new("lock",         "Lock",         Icons.Material.Filled.Lock,             "#263238"),
        new("key",          "Key",          Icons.Material.Filled.Key,              "#F57F17"),
        new("place",        "Place",        Icons.Material.Filled.Place,            "#FF5252"),
        new("map",          "Map",          Icons.Material.Filled.Map,              "#43A047"),
        new("wifi",         "Wifi",         Icons.Material.Filled.Wifi,             "#03A9F4"),
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
