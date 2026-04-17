using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.CapturePoint.UI.MedievalWindow;

public sealed class MedievalButton : Button
{
    private static readonly Color GoldBorder = Color.FromHex("#d4af37");
    private static readonly Color HoverGold = Color.FromHex("#ffdf7a");
    private static readonly Color MedievalDark = Color.FromHex("#2a1f18");
    private static readonly Color TextNormal = Color.FromHex("#a89f91");
    private static readonly Color TextDisabled = Color.FromHex("#4a4540");
    private static readonly Color BorderDisabled = Color.FromHex("#3d3530");
    private static readonly Color BgDisabled = Color.FromHex("#1a1410");

    private readonly StyleBoxFlat _normalBox;
    private readonly StyleBoxFlat _disabledBox;

    public MedievalButton()
    {
        Label.FontColorOverride = TextNormal;
        Margin = new Thickness(2);

        _normalBox = new StyleBoxFlat
        {
            BackgroundColor = MedievalDark,
            BorderColor = GoldBorder,
            BorderThickness = new Thickness(2)
        };

        var hoverBox = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#3d2e24"),
            BorderColor = HoverGold,
            BorderThickness = new Thickness(2)
        };

        _disabledBox = new StyleBoxFlat
        {
            BackgroundColor = BgDisabled,
            BorderColor = BorderDisabled,
            BorderThickness = new Thickness(2)
        };

        StyleBoxOverride = _normalBox;

        OnMouseEntered += _ =>
        {
            if (Disabled)
                return;

            StyleBoxOverride = hoverBox;
            Label.FontColorOverride = Color.White;
        };

        OnMouseExited += _ =>
        {
            if (Disabled)
                return;
            StyleBoxOverride = _normalBox;
            Label.FontColorOverride = TextNormal;
        };
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (_normalBox == null || _disabledBox == null)
            return;

        if (Disabled)
        {
            StyleBoxOverride = _disabledBox;
            Label.FontColorOverride = TextDisabled;
        }
        else
        {
            StyleBoxOverride = _normalBox;
            Label.FontColorOverride = TextNormal;
        }
    }
}
