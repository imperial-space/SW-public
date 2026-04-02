using Content.Shared.Imperial.Medieval.Ships.Sail;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.Sail;

[UsedImplicitly]
public sealed class SailMenuUIController : UIController
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IEntityNetworkManager _net = default!;

    private SimpleRadialMenu? _menu;
    public enum Direction
    {
        Left,
        Right
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<OpenSailMenuEvent>(OpenSailMenu);
    }

    private void OpenSailMenu(OpenSailMenuEvent args, EntitySessionEventArgs entArgs)
    {
        if (_menu != null)
            return; // Не открывать, если уже открыто

        var options = new List<RadialMenuOptionBase>
        {
            new RadialMenuActionOption<int>(HandleRotateLeft, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-rotate-left")
            },
            new RadialMenuActionOption<int>(HandleToggleFold, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-toggle-fold")
            },
            new RadialMenuActionOption<int>(HandleRotateRight, args.Target)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-rotate-right")
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
        var msg = new RotateSailEvent(1, target);
        _net.SendSystemNetworkMessage(msg);
    }

    private void HandleToggleFold(int target) // bool — фейковый аргумент
    {
        var msg = new RotateSailEvent(0, target);
        _net.SendSystemNetworkMessage(msg);
    }

    private void HandleRotateRight(int target)
    {
        var msg = new RotateSailEvent(-1, target);
        _net.SendSystemNetworkMessage(msg);
    }

}
