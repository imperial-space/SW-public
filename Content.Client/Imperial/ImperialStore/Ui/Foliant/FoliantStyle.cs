using Content.Client.Imperial.Medieval.UserInterface.Elements;
using Content.Client.Resources;
using Content.Shared.Imperial.ImperialStore;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.ImperialStore.Ui.Foliant;

/// <summary>
///     imperial medieval - colours, fonts and textures of the grimoire window. The textures are generated
///     (see <c>Resources/Textures/Imperial/Medieval/Interface/Foliant</c>) and tinted here, so a school needs no art of its own.
/// </summary>
public static class FoliantStyle
{
    private const string TexturePath = "/Textures/Imperial/Medieval/Interface/Foliant/";
    private const string FontPath = "/Fonts/Imperial/Vinque/Vinque.otf";

    // ink on the pages
    public static readonly Color Ink = Color.FromHex("#3a2a1c");
    public static readonly Color InkRed = Color.FromHex("#6b2a1a");
    public static readonly Color InkFaded = Color.FromHex("#7d6a4a");
    public static readonly Color RowHover = Color.FromHex("#d2bf96");
    public static readonly Color RowLine = Color.FromHex("#b39c72");
    public static readonly Color SearchFill = Color.FromHex("#bda67b");
    public static readonly Color LinkHover = Color.FromHex("#a3401f");
    public static readonly Color RitualLine = Color.FromHex("#8c7552");

    // text on the dark parts: ribbons, buttons
    public static readonly Color Cloth = Color.FromHex("#f0e6d0");
    public static readonly Color Gold = MedievalUiStyle.Gold;
    public static readonly Color GoldBright = MedievalUiStyle.GoldBright;
    public static readonly Color Faded = MedievalUiStyle.TextFaded;
    public static readonly Color Price = Color.FromHex("#ffb070");
    public static readonly Color PriceMissing = Color.FromHex("#e0664a");

    /// <summary>
    /// The colours a theme changes. A theme that is not listed here is the common look.
    /// </summary>
    /// <param name="SearchLine">The line of the search box, the colour of the school when empty.</param>
    /// <param name="Hint">The faded words: hints, the name of the seals.</param>
    /// <param name="TabSaturation">How much of the colour of the school stays in the cloth of a ribbon.</param>
    /// <param name="ButtonText">The words on a button and on a pressed one.</param>
    public sealed record Palette(Color? SearchLine, Color Hint, float TabSaturation, Color ButtonText, Color ButtonTextHot);

    private static readonly Palette CommonPalette = new(null, InkFaded, 1f, Gold, GoldBright);

    private static readonly Dictionary<string, Palette> Palettes = new()
    {
        // the necromancer: a dark brown line of the search, darker hints, dull ribbons, blood-red words on the buttons
        ["necro"] = new Palette(Color.FromHex("#6b5a40"), Color.FromHex("#5a4a32"), 0.7f, Color.FromHex("#b8322a"), Color.FromHex("#e0584a")),
        // the wanderer: the cloth of the ribbons has faded in the sun
        ["wanderer"] = new Palette(null, InkFaded, 0.75f, Gold, GoldBright),
    };

    public static Palette PaletteOf(string theme) => Palettes.TryGetValue(theme, out var palette) ? palette : CommonPalette;

    // brightness of the ribbon texture, the tint is divided by it
    private const float RibbonBase = 0.8f;

    public static Font Vinque(IResourceCache cache, int size) => cache.GetFont(FontPath, size);

    public static Texture Tex(IResourceCache cache, string file) => cache.GetTexture(TexturePath + file);

    /// <summary>
    ///     The texture of a theme (a folder inside the Foliant one, e.g. <c>archmage</c>). A theme paints only what it needs,
    ///     the rest is taken from the common textures.
    /// </summary>
    public static Texture Tex(IResourceCache cache, string theme, string file)
    {
        if (TryTheme(cache, theme, file, out var texture))
            return texture;

        return Tex(cache, file);
    }

    // a theme does not have every texture, a missing one is looked for once and not on every row of the list
    private static readonly Dictionary<(string Theme, string File), Texture?> ThemeTextures = new();

    /// <summary>The texture of a theme, if the theme has one of its own.</summary>
    public static bool TryTheme(IResourceCache cache, string theme, string file, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Texture? texture)
    {
        texture = null;
        if (string.IsNullOrEmpty(theme))
            return false;

        if (!ThemeTextures.TryGetValue((theme, file), out texture))
        {
            texture = cache.TryGetResource<TextureResource>(new ResPath(TexturePath + theme + "/" + file), out var resource)
                ? resource.Texture
                : null;
            ThemeTextures[(theme, file)] = texture;
        }

        return texture != null;
    }

    private static ImperialSchoolData? SchoolOf(string? currency)
    {
        return ImperialStoreSlots.TryGetSchool(IoCManager.Resolve<IPrototypeManager>(), currency, out var data) ? data : null;
    }

    /// <summary>Colour of a school in the window.</summary>
    public static Color School(string? currency, Color fallback)
    {
        return SchoolOf(currency)?.WindowColor ?? fallback;
    }

    /// <summary>The colour of a school that is dark enough for lines and text on a page.</summary>
    public static Color Accent(string? currency, Color school)
    {
        if (currency == null)
            return Ink;

        if (SchoolOf(currency)?.ClothColor is { } cloth)
            return cloth;

        return new Color(school.R * 0.75f, school.G * 0.75f, school.B * 0.75f);
    }

    /// <summary>Tint for the ribbon texture of a school.</summary>
    public static Color RibbonTint(string? currency, Color school, float saturation = 1f)
    {
        var cloth = SchoolOf(currency)?.ClothColor ?? new Color(school.R * 0.55f, school.G * 0.55f, school.B * 0.55f);

        if (saturation < 1f)
        {
            // towards the grey of the same brightness
            var grey = cloth.R * 0.3f + cloth.G * 0.59f + cloth.B * 0.11f;
            cloth = new Color(
                grey + (cloth.R - grey) * saturation,
                grey + (cloth.G - grey) * saturation,
                grey + (cloth.B - grey) * saturation);
        }

        return new Color(
            Math.Min(1f, cloth.R / RibbonBase),
            Math.Min(1f, cloth.G / RibbonBase),
            Math.Min(1f, cloth.B / RibbonBase));
    }

    public static StyleBoxTexture Patch(IResourceCache cache, string file, float patch, float content, string theme = "")
    {
        var box = new StyleBoxTexture
        {
            Texture = Tex(cache, theme, file),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        box.SetPatchMargin(StyleBox.Margin.All, patch);
        box.SetContentMarginOverride(StyleBox.Margin.All, content);
        return box;
    }

    public static StyleBoxTexture Cover(IResourceCache cache, string theme = "") => Patch(cache, "cover.png", 16, 16, theme);

    public static StyleBoxTexture Page(IResourceCache cache, bool left, string theme = "") => Patch(cache, left ? "page_l.png" : "page_r.png", 20, 18, theme);

    /// <summary>The fold between the pages, repeats down the book.</summary>
    public static StyleBoxTexture Gutter(IResourceCache cache, string theme = "")
    {
        // the top and the bottom are the edge of a page, only the middle is tiled
        var box = new StyleBoxTexture
        {
            Texture = Tex(cache, theme, "gutter.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        box.SetPatchMargin(StyleBox.Margin.Vertical, 20);
        return box;
    }

    /// <summary>A ribbon. The middle repeats, so a taller (chosen) ribbon is just longer.</summary>
    public static StyleBoxTexture Ribbon(IResourceCache cache, Color tint, string theme = "")
    {
        var box = new StyleBoxTexture
        {
            Texture = Tex(cache, theme, "ribbon.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
            Modulate = tint,
        };
        box.SetPatchMargin(StyleBox.Margin.Top, 8);
        box.SetPatchMargin(StyleBox.Margin.Bottom, 13);
        box.SetContentMarginOverride(StyleBox.Margin.Left, 4);
        box.SetContentMarginOverride(StyleBox.Margin.Right, 6);
        box.SetContentMarginOverride(StyleBox.Margin.Top, 6);
        box.SetContentMarginOverride(StyleBox.Margin.Bottom, 15);
        return box;
    }

    /// <summary>
    ///     The trim of a ribbon of a theme (stitches, a hem), drawn over the tinted ribbon so that it keeps its own colour.
    ///     Null when the theme has none.
    /// </summary>
    public static StyleBoxTexture? RibbonTrim(IResourceCache cache, string theme)
    {
        if (!TryTheme(cache, theme, "ribbon_trim.png", out var texture))
            return null;

        var box = new StyleBoxTexture
        {
            Texture = texture,
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        box.SetPatchMargin(StyleBox.Margin.Top, 8);
        box.SetPatchMargin(StyleBox.Margin.Bottom, 13);
        return box;
    }

    public static StyleBoxTexture Button(IResourceCache cache, string file, string theme = "") => Patch(cache, file, 5, 4, theme);

    /// <summary>A flat tinted panel, e.g. a row of a list.</summary>
    public static StyleBoxFlat Flat(Color background, Color border, float thickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(thickness),
        };
    }

    public static string RuneFile(string school) => SchoolOf(school)?.Rune ?? "rune_none.png";

    /// <summary>The sign of a school (13x13 pixels, <paramref name="scale"/> times bigger), a plain diamond for anything else.</summary>
    public static TextureRect Rune(IResourceCache cache, string? school, Color color, float scale)
    {
        return new TextureRect
        {
            Texture = Tex(cache, school == null ? "rune_none.png" : RuneFile(school)),
            TextureScale = new System.Numerics.Vector2(scale, scale),
            Stretch = TextureRect.StretchMode.Keep,
            ModulateSelfOverride = color,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    /// <summary>A wax seal, one for every point of archmage.</summary>
    public static TextureRect Seal(IResourceCache cache, string theme = "")
    {
        return new TextureRect
        {
            Texture = Tex(cache, theme, "seal.png"),
            TextureScale = new System.Numerics.Vector2(2, 2),
            Stretch = TextureRect.StretchMode.Keep,
        };
    }

    public static string SchoolName(string school) => SchoolOf(school) is { } data ? Loc.GetString(data.Name) : school;

    public static Label Text(string text, Font? font, Color color)
    {
        var label = new Label { Text = text, FontColorOverride = color };
        if (font != null)
            label.FontOverride = font;

        return label;
    }
}
