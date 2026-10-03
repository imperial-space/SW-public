using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.UserInterface.Elements;

/// <summary>
///     imperial medieval - palette and lettering shared by the reworked medieval windows.
///     Colours are fields rather than constants so XAML can reach them through <c>x:Static</c>.
/// </summary>
public static class MedievalUiStyle
{
    public static readonly Color Background = Color.FromHex("#2a1f18");
    public static readonly Color BackgroundHover = Color.FromHex("#3d2e24");
    public static readonly Color BackgroundPressed = Color.FromHex("#5a3b1c");
    public static readonly Color BackgroundDisabled = Color.FromHex("#1a1410");

    /// <summary>Recessed areas inside a window, e.g. the campfire pot.</summary>
    public static readonly Color Inset = Color.FromHex("#150f0c");

    public static readonly Color Border = Color.FromHex("#5c4a3d");
    public static readonly Color BorderDisabled = Color.FromHex("#3d3530");

    public static readonly Color Gold = Color.FromHex("#d4af37");
    public static readonly Color GoldBright = Color.FromHex("#ffdf7a");

    public static readonly Color Text = Color.FromHex("#a89f91");
    public static readonly Color TextFaded = Color.FromHex("#7a6a58");
    public static readonly Color TextDisabled = Color.FromHex("#4a4540");

    /// <summary>Something in the way, e.g. an ingredient the mortar can't take.</summary>
    public static readonly Color Warning = Color.FromHex("#8a3a2a");

    /// <summary>Enchanted things at work, e.g. the mortar grinding on its own.</summary>
    public static readonly Color Magic = Color.FromHex("#b89ae8");

    /// <summary>Medieval lettering for titles and buttons. Small print stays in the regular font to stay readable.</summary>
    private const string FontPath = "/Fonts/Imperial/Vinque/Vinque.otf";

    private const int TitleFontSize = 18;
    private const int TextFontSize = 14;

    public static Font TitleFont(IResourceCache cache) => cache.GetFont(FontPath, TitleFontSize);

    public static Font TextFont(IResourceCache cache) => cache.GetFont(FontPath, TextFontSize);

    /// <summary>Puts the medieval lettering on labels and on the text of buttons.</summary>
    public static void ApplyTextFont(IResourceCache cache, params Control[] controls)
    {
        var font = TextFont(cache);

        foreach (var control in controls)
        {
            switch (control)
            {
                case Button button:
                    button.Label.FontOverride = font;
                    break;
                case Label label:
                    label.FontOverride = font;
                    break;
            }
        }
    }
}
