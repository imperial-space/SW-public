using System.Collections.Generic;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Helm;

[UsedImplicitly]
public sealed class HelmMenuUIController : UIController
{
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    private SimpleRadialMenu? _menu;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OpenHelmMenuEvent>(OpenHelmMenu);
    }

    private void OpenHelmMenu(OpenHelmMenuEvent args, EntitySessionEventArgs entArgs)
    {
        if (_menu != null)
            return;

        var options = new List<RadialMenuOptionBase>
        {
            new RadialMenuActionOption<int>(HandleRotateLeft, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("helm-menu-rotate-left")
            },
            new RadialMenuActionOption<int>(HandleCenter, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png"))),
                ToolTip = Loc.GetString("helm-menu-center")
            },
            new RadialMenuActionOption<int>(HandleRotateRight, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("helm-menu-rotate-right")
            }
        };

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
        _menu.OnClose += OnMenuClosed;
    }

    private void OnMenuClosed()
    {
        _menu?.Dispose();
        _menu = null;
    }

    private void HandleRotateLeft(int target)
    {
        _net.SendSystemNetworkMessage(new HelmMenuActionEvent(HelmMenuAction.RotateLeft, target));
    }

    private void HandleCenter(int target)
    {
        _net.SendSystemNetworkMessage(new HelmMenuActionEvent(HelmMenuAction.Center, target));
    }

    private void HandleRotateRight(int target)
    {
        _net.SendSystemNetworkMessage(new HelmMenuActionEvent(HelmMenuAction.RotateRight, target));
    }
}
