using Content.Client.Imperial.Medieval.UserInterface.Elements;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client.Imperial.Medieval.UserInterface.Windows;

/// <summary>
///     imperial medieval - look of <see cref="MedievalWorkbenchMenu"/>, picked by <c>uiTheme</c> on <c>CP14Workbench</c>.
///     New look: a case in <see cref="Get"/> plus a method like <see cref="Leather"/>;
///     textures in <c>/Textures/Imperial/Medieval/Interface/Workbench/&lt;Name&gt;/</c>.
/// </summary>
public sealed class MedievalWorkbenchTheme
{
    /// <summary>Null keeps the window's own header.</summary>
    public StyleBox? Header { get; private init; }

    public float HeaderHeight { get; private init; } = 32;

    public StyleBox List { get; private init; } = default!;

    public StyleBox Details { get; private init; } = default!;

    public StyleBox Preview { get; private init; } = default!;

    public StyleBox Material { get; private init; } = default!;

    public Color Selected { get; private init; }

    /// <summary>Drawn over both ends of the craft button when it can be pressed, null for none.</summary>
    public Texture? ButtonNail { get; private init; }

    /// <summary>Tiled over the craft button when it can be pressed, null for none.</summary>
    public StyleBox? ButtonFabric { get; private init; }

    // grey-blue so brown, pale and dark items all stand out
    private static readonly Color PreviewColor = Color.FromHex("#4e565e");

    // outline of the preview frame in the blue and dark themes
    private static readonly Color Edge = Color.FromHex("#090b10");

    // outline of the list and details panels
    private static readonly Color Rim = MedievalUiStyle.BackgroundHover;

    public static MedievalWorkbenchTheme Get(string? id, IResourceCache cache)
    {
        return id switch
        {
            "Leather" => Leather(cache),
            "Wood" => Wood(cache),
            "Blacksmith" => Blacksmith(cache),
            "Tailor" => Tailor(cache),
            "Foundry" => Foundry(cache),
            "Magic" => Magic(cache),
            _ => Plain(),
        };
    }

    public static MedievalWorkbenchTheme Plain()
    {
        return new MedievalWorkbenchTheme
        {
            List = Flat(MedievalUiStyle.Inset, MedievalUiStyle.Border, 6),
            Details = Flat(MedievalUiStyle.Inset, MedievalUiStyle.Border, 12),
            Preview = Flat(PreviewColor, MedievalUiStyle.Border, 0, 2),
            Material = Flat(MedievalUiStyle.Background, MedievalUiStyle.Background, 4),
            Selected = MedievalUiStyle.BackgroundHover,
        };
    }

    public static MedievalWorkbenchTheme Leather(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Leather/";
        var edge = Color.FromHex("#140c07");

        // 128x40, seam and edge in the bottom 10 px
        var header = HeaderStrip(cache, path);

        var list = new StyleBoxTexture
        {
            Texture = cache.GetTexture(path + "piece.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        list.SetPatchMargin(StyleBox.Margin.All, 12);
        list.SetContentMarginOverride(StyleBox.Margin.All, 6);

        // 40x36, the point takes the left 14 px
        var tag = Tag(cache, path, 14);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 40,
            List = list,
            Details = Flat(MedievalUiStyle.Inset, edge, 12),
            Preview = Flat(PreviewColor, edge, 0, 2),
            Material = tag,
            Selected = Color.FromHex("#4a2f1c"),
        };
    }

    public static MedievalWorkbenchTheme Wood(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Wood/";

        // 128x40, light top edge, seams between the boards, dark foot in the bottom 10 px
        var header = HeaderStrip(cache, path);

        // 32x32, an iron nail in each corner
        var list = Panel(cache.GetTexture(path + "panel.png"), 10, 8);
        var details = Panel(cache.GetTexture(path + "panel.png"), 10, 12);

        // 24x24, the nails sit inside the corner patches
        var preview = Panel(cache.GetTexture(path + "preview.png"), 10, 0);

        // 40x36, the nail takes the left 14 px
        var plank = Tag(cache, path, 14);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 40,
            List = list,
            Details = details,
            Preview = preview,
            Material = plank,
            Selected = Color.FromHex("#4b3320"),
            ButtonNail = cache.GetTexture(path + "nail.png"),
        };
    }

    public static MedievalWorkbenchTheme Blacksmith(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Blacksmith/";
        var edge = Color.FromHex("#0b0c0e");

        // 288x40, charred boards with an iron strap, dark foot in the bottom 10 px
        var header = HeaderStrip(cache, path);

        // 40x36, a flat iron strip takes the left 8 px
        var tag = Tag(cache, path, 8);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 40,
            List = Flat(MedievalUiStyle.Inset, edge, 6),
            Details = Flat(MedievalUiStyle.Inset, edge, 12),
            Preview = Flat(PreviewColor, edge, 0, 2),
            Material = tag,
            Selected = Color.FromHex("#3a2e27"),
        };
    }

    public static MedievalWorkbenchTheme Tailor(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Tailor/";

        // 128x44, indigo weave, a band of gold thread under the title, dark foot in the bottom 10 px
        var header = HeaderStrip(cache, path);

        // 8x4 translucent weave, tiled over the craft button
        var fabric = new StyleBoxTexture
        {
            Texture = cache.GetTexture(path + "weave.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
        };

        // 42x36, a cloth swatch: the pinked edge takes the left 14 px
        var swatch = Tag(cache, path, 14);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 44,
            List = Flat(MedievalUiStyle.Inset, Rim, 6),
            Details = Flat(MedievalUiStyle.Inset, Rim, 12),
            Preview = Flat(PreviewColor, Edge, 0, 2),
            Material = swatch,
            Selected = Color.FromHex("#242a38"),
            ButtonFabric = fabric,
        };
    }

    public static MedievalWorkbenchTheme Foundry(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Foundry/";

        // 132x44, sooted brick fading into the window, edge in the last row
        var header = HeaderStrip(cache, path);

        // 40x36, a brick plaque: the outline sits inside the 4 px patches
        var plaque = Tag(cache, path, 4);

        // flat iron plate with an amber edge, drawn over the craft button when it can be pressed
        var plate = Flat(Color.FromHex("#2a2e34"), Color.FromHex("#a85a22"), 0);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 44,
            List = Flat(MedievalUiStyle.Inset, Rim, 6),
            Details = Flat(MedievalUiStyle.Inset, Rim, 12),
            Preview = Flat(PreviewColor, Edge, 0, 2),
            Material = plaque,
            Selected = Color.FromHex("#4a2519"),
            ButtonFabric = plate,
        };
    }

    public static MedievalWorkbenchTheme Magic(IResourceCache cache)
    {
        const string path = "/Textures/Imperial/Medieval/Interface/Workbench/Magic/";

        // 176x44, four violet plates with a dim sigil each, edge in the last row
        var header = HeaderStrip(cache, path);

        // 24x24, cut corners with a violet line inside the outline
        var list = Panel(cache.GetTexture(path + "panel.png"), 8, 7);
        var details = Panel(cache.GetTexture(path + "panel.png"), 8, 12);

        // 40x36, a cut plaque: the gem takes the left 14 px
        var plaque = Tag(cache, path, 14);

        // flat violet plate with a cyan edge, drawn over the craft button when it can be pressed
        var plate = Flat(Color.FromHex("#241a44"), Color.FromHex("#2a7f9c"), 0);

        return new MedievalWorkbenchTheme
        {
            Header = header,
            HeaderHeight = 44,
            List = list,
            Details = details,
            Preview = Flat(PreviewColor, Edge, 0, 2),
            Material = plaque,
            Selected = Color.FromHex("#34286a"),
            ButtonNail = cache.GetTexture(path + "gem.png"),
            ButtonFabric = plate,
        };
    }

    /// <summary>The same header strip in every theme: top row, body, seam and edge in the bottom 10 px.</summary>
    private static StyleBoxTexture HeaderStrip(IResourceCache cache, string path)
    {
        return new StyleBoxTexture
        {
            Texture = cache.GetTexture(path + "header.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
            PatchMarginTop = 1,
            PatchMarginBottom = 10,
        };
    }

    /// <summary>Plaque under a material: <paramref name="leftPatch"/> px on the left hold the nail, point or gem.</summary>
    private static StyleBoxTexture Tag(IResourceCache cache, string path, float leftPatch)
    {
        var box = new StyleBoxTexture
        {
            Texture = cache.GetTexture(path + "tag.png"),
            Mode = StyleBoxTexture.StretchMode.Tile,
            PatchMarginLeft = leftPatch,
            PatchMarginRight = 4,
            PatchMarginTop = 4,
            PatchMarginBottom = 4,
        };
        box.SetContentMarginOverride(StyleBox.Margin.Left, 16);
        box.SetContentMarginOverride(StyleBox.Margin.Right, 8);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
        return box;
    }

    private static StyleBoxTexture Panel(Texture texture, float patch, float padding)
    {
        var box = new StyleBoxTexture
        {
            Texture = texture,
            Mode = StyleBoxTexture.StretchMode.Tile,
        };
        box.SetPatchMargin(StyleBox.Margin.All, patch);
        box.SetContentMarginOverride(StyleBox.Margin.All, padding);
        return box;
    }

    private static StyleBoxFlat Flat(Color background, Color border, float padding, float borderThickness = 1)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(borderThickness),
        };
        box.SetContentMarginOverride(StyleBox.Margin.All, padding);
        return box;
    }
}
