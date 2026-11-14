using System.Numerics;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossArenaSpectatorWindow : DefaultWindow
{
    private readonly ScalingViewport _view;

    public BossArenaSpectatorWindow()
    {
        Title = Loc.GetString("Boss_Encounter_Spectator_Title");
        Resizable = true;
        MinSize = new Vector2(400, 440);
        SetSize = new Vector2(660, 700);
        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        layout.AddChild(new Label { Text = Loc.GetString("Boss_Encounter_Spectator_Help") });
        _view = new ScalingViewport
        {
            ViewportSize = new Vector2i(640, 640),
            MinSize = new Vector2(384, 384),
            HorizontalExpand = true,
            VerticalExpand = true,
            StretchMode = ScalingViewportStretchMode.Nearest,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        layout.AddChild(_view);
        Contents.AddChild(layout);
    }

    public void SetEye(IEye eye) => _view.Eye = eye;
}
