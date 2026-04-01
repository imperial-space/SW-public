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

    private SimpleRadialMenu? _menu;
    public enum Direction
    {
        Left,
        Right
    }

    public override void Initialize()
    {
        SubscribeNetworkEvent<OpenSailMenuEvent>(OpenSailMenu);
    }

    private void OpenSailMenu(OpenSailMenuEvent args, EntitySessionEventArgs entArgs)
    {
        if (_menu != null) return; // Не открывать, если уже открыто

        var options = new List<RadialMenuOptionBase>
        {
            new RadialMenuActionOption<Direction>(HandleRotateLeft, Direction.Left)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/rotate_left.png"))),
                ToolTip = Loc.GetString("sail-menu-rotate-left")
            },
            new RadialMenuActionOption<bool>(HandleToggleFold, true)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/fold.png"))),
                ToolTip = Loc.GetString("sail-menu-toggle-fold")
            },
            new RadialMenuActionOption<Direction>(HandleRotateRight, Direction.Right)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Emotes/rotate_right.png"))),
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

    private void HandleRotateLeft(Direction direction)
    {
        EntityManager.RaisePredictiveEvent(new RotateSailEvent(-1, ));
    }

    private void HandleToggleFold(bool _) // bool — фейковый аргумент
    {
        EntityManager.RaisePredictiveEvent(new RotateSailEvent(0));
    }

    private void HandleRotateRight(Direction direction)
    {
        EntityManager.RaisePredictiveEvent(new RotateSailEvent(1));
    }

}
