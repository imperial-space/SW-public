using Content.Shared.Imperial.Medieval.Boss;
using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client.Imperial.Medieval.Boss.HealthBar;

public static class BossHudLayout
{
    public static float Scale(IConfigurationManager config, float availableWidth = 0, float baseWidth = 420)
    {
        var scale = config.GetCVar(BossHudCVars.Scale);
        scale = float.IsFinite(scale) ? Math.Clamp(scale, 0.5f, 2) : 1;          // keep the saved scale within safe limits
        return float.IsFinite(availableWidth) && availableWidth > 0 && baseWidth > 0
            ? Math.Min(scale, availableWidth / baseWidth) : scale;
    }

    public static Vector2 Position(Vector2 parent, Vector2 size, float horizontal, float vertical)
    {
        horizontal = float.IsFinite(horizontal) ? Math.Clamp(horizontal, 0, 1) : 0.5f;
        vertical = float.IsFinite(vertical) ? Math.Clamp(vertical, 0, 1) : 0.85f;
        return Vector2.Max(Vector2.Zero, parent - size) * new Vector2(horizontal, vertical); // keep the whole bar inside its parent
    }

    public static void ApplyPosition(Control bar, IConfigurationManager config)
    {
        if (bar.Parent == null) return;
        var size = Vector2.Max(bar.DesiredSize, bar.MinSize);
        var position = Position(bar.Parent.Size, size, config.GetCVar(BossHudCVars.PositionX),
            config.GetCVar(BossHudCVars.PositionY));
        LayoutContainer.SetAnchorPreset(bar, LayoutContainer.LayoutPreset.TopLeft);
        LayoutContainer.SetGrowHorizontal(bar, LayoutContainer.GrowDirection.End);
        LayoutContainer.SetGrowVertical(bar, LayoutContainer.GrowDirection.End);
        LayoutContainer.SetMarginLeft(bar, position.X);
        LayoutContainer.SetMarginTop(bar, position.Y);
        LayoutContainer.SetMarginRight(bar, position.X);
        LayoutContainer.SetMarginBottom(bar, position.Y);
    }
}
