// SPDX-License-Identifier: BUSL-1.1
using MudBlazor;

namespace ProjectJA.Host.Theming;

/// <summary>One selectable theme preset (DB stores only the Key). Swatches are
/// hex strings for the picker preview, in primary/secondary/accent order.</summary>
public sealed record ThemeOption(string Key, string Name, string[] Swatches);

/// <summary>
/// The fixed set of UI themes a user can choose from. MudBlazor types live
/// only here in the Host — modules persist the string key, never a MudTheme,
/// so the modular-monolith boundary stays clean. 15 curated presets; each
/// carries its own light AND dark palette (the dark variant is controlled by
/// the separate per-user dark-mode toggle).
/// </summary>
public sealed class ThemeCatalog
{
    public const string DefaultKey = "default";

    private readonly Dictionary<string, MudTheme> _themes;

    public IReadOnlyList<ThemeOption> Options { get; }

    public ThemeCatalog()
    {
        // Build(lightPrimary, lightSecondary, lightAppbar,
        //       darkPrimary, darkSecondary, darkAppbar, darkBg, darkSurface)
        _themes = new Dictionary<string, MudTheme>(StringComparer.Ordinal)
        {
            ["default"]   = Build("#594ae2", "#ff4081", "#594ae2", "#7c6ff0", "#ff79b0", "#1e1e2f", "#1a1a27", "#24243a"),
            ["ocean"]     = Build("#0277bd", "#00bfa5", "#0277bd", "#4fc3f7", "#1de9b6", "#0b2027", "#0b2027", "#13313b"),
            ["forest"]    = Build("#2e7d32", "#7cb342", "#2e7d32", "#81c784", "#c5e1a5", "#14241a", "#14241a", "#1d3326"),
            ["dracula"]   = Build("#7e57c2", "#ec407a", "#44475a", "#bd93f9", "#ff79c6", "#282a36", "#282a36", "#343746"),
            ["contrast"]  = BuildContrast(),
            ["nord"]      = Build("#5e81ac", "#88c0d0", "#4c566a", "#88c0d0", "#81a1c1", "#2e3440", "#2e3440", "#3b4252"),
            ["solarized"] = Build("#268bd2", "#2aa198", "#073642", "#268bd2", "#2aa198", "#002b36", "#002b36", "#073642"),
            ["monokai"]   = Build("#66bb6a", "#e91e63", "#272822", "#a6e22e", "#f92672", "#272822", "#272822", "#3e3d32"),
            ["gruvbox"]   = Build("#b57614", "#cc241d", "#3c3836", "#fabd2f", "#fb4934", "#282828", "#282828", "#3c3836"),
            ["crimson"]   = Build("#c62828", "#ff8a65", "#c62828", "#ef5350", "#ffab91", "#2a1414", "#2a1414", "#3a1d1d"),
            ["sunset"]    = Build("#ef6c00", "#ffb300", "#ef6c00", "#ffa726", "#ffd54f", "#2a1a0a", "#2a1a0a", "#3a2510"),
            ["grape"]     = Build("#6a1b9a", "#ab47bc", "#6a1b9a", "#ba68c8", "#ce93d8", "#1f0f29", "#1f0f29", "#2c1539"),
            ["slate"]     = Build("#455a64", "#78909c", "#37474f", "#90a4ae", "#b0bec5", "#1c252a", "#1c252a", "#263238"),
            ["rose"]      = Build("#c2185b", "#f06292", "#c2185b", "#f06292", "#f8bbd0", "#2a0e1b", "#2a0e1b", "#3a1526"),
            ["midnight"]  = Build("#1a237e", "#00bcd4", "#1a237e", "#3f51b5", "#00e5ff", "#0d1117", "#0d1117", "#161b22"),
        };

        Options = new List<ThemeOption>
        {
            new("default",   "ProjectJA (Indigo)", ["#594ae2", "#ff4081", "#594ae2"]),
            new("ocean",     "Ocean",              ["#0277bd", "#00bfa5", "#0277bd"]),
            new("forest",    "Forest",             ["#2e7d32", "#7cb342", "#2e7d32"]),
            new("dracula",   "Dracula",            ["#7e57c2", "#ec407a", "#44475a"]),
            new("contrast",  "High Contrast",      ["#000000", "#1565c0", "#ffeb3b"]),
            new("nord",      "Nord",               ["#5e81ac", "#88c0d0", "#2e3440"]),
            new("solarized", "Solarized",          ["#268bd2", "#2aa198", "#b58900"]),
            new("monokai",   "Monokai",            ["#a6e22e", "#f92672", "#66d9ef"]),
            new("gruvbox",   "Gruvbox",            ["#d79921", "#cc241d", "#458588"]),
            new("crimson",   "Crimson",            ["#c62828", "#ff8a65", "#c62828"]),
            new("sunset",    "Sunset",             ["#ef6c00", "#ffb300", "#ef6c00"]),
            new("grape",     "Grape",              ["#6a1b9a", "#ab47bc", "#6a1b9a"]),
            new("slate",     "Slate",              ["#455a64", "#78909c", "#37474f"]),
            new("rose",      "Rose",               ["#c2185b", "#f06292", "#c2185b"]),
            new("midnight",  "Midnight",           ["#1a237e", "#00bcd4", "#1a237e"]),
        };
    }

    public bool IsKnown(string? key) => key is not null && _themes.ContainsKey(key);

    /// <summary>Returns the theme for the key, or the default if null/unknown.</summary>
    public MudTheme Resolve(string? key) =>
        key is not null && _themes.TryGetValue(key, out var theme)
            ? theme
            : _themes[DefaultKey];

    // Shared across every theme so a label like "MEMBERSHIP" fits inside a
    // standard MudButton without truncation. MudBlazor's default Button
    // typography is 0.875rem; 0.75rem buys ~14% horizontal room with no
    // visible "shrunken UI" effect at normal zoom levels.
    private static readonly Typography SharedTypography = new()
    {
        Button = new ButtonTypography { FontSize = "0.75rem" },
    };

    private static MudTheme Build(
        string lightPrimary, string lightSecondary, string lightAppbar,
        string darkPrimary, string darkSecondary, string darkAppbar,
        string darkBackground, string darkSurface) => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = lightPrimary,
            Secondary = lightSecondary,
            AppbarBackground = lightAppbar,
            AppbarText = "#ffffff",
        },
        PaletteDark = new PaletteDark
        {
            Primary = darkPrimary,
            Secondary = darkSecondary,
            AppbarBackground = darkAppbar,
            AppbarText = "#ffffff",
            Background = darkBackground,
            Surface = darkSurface,
            DrawerBackground = darkBackground,
        },
        Typography = SharedTypography,
    };

    private static MudTheme BuildContrast() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#000000",
            Secondary = "#1565c0",
            AppbarBackground = "#000000",
            AppbarText = "#ffeb3b",
            Background = "#ffffff",
            Surface = "#ffffff",
            TextPrimary = "#000000",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#ffeb3b",
            Secondary = "#80d8ff",
            AppbarBackground = "#000000",
            AppbarText = "#ffeb3b",
            Background = "#000000",
            Surface = "#0a0a0a",
            TextPrimary = "#ffffff",
            DrawerBackground = "#000000",
        },
        Typography = SharedTypography,
    };
}
