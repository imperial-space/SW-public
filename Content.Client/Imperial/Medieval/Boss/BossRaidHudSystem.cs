using Content.Shared.Imperial.Medieval.Boss;
using System.Numerics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Boss;

public sealed class BossRaidHudSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    private BossRaidHudControl? _hud;
    private LayoutContainer? _root;
    private NetEntity? _arena;

    public override void Initialize()
    {
        SubscribeNetworkEvent<BossRaidHudEvent>(OnHud);
    }

    private void OnHud(BossRaidHudEvent args)
    {
        _arena = args.Arena;                                                     // remember which arena this roster belongs to
        if (_arena == null && _hud == null) return;
        if (_hud == null || _hud.Disposed || _root == null || _root.Disposed)
        {
            _hud = new BossRaidHudControl();
            _root = new RaidHudLayer(_hud) { MouseFilter = Control.MouseFilterMode.Ignore, InheritChildMeasure = false };
            LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);
            _ui.WindowRoot.AddChild(_root);
            _root.AddChild(_hud);
        }
        _hud.SetMembers(args.Members);
        UpdateVisibility();
    }

    public override void FrameUpdate(float frameTime) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (_hud == null || _hud.Disposed || _root == null || _root.Disposed) return;
        if (_root.Parent == null) _ui.WindowRoot.AddChild(_root);
        _hud.Visible = _arena != null && _players.LocalEntity is { } local &&
                       TryComp(local, out TransformComponent? xform) && xform.MapUid is { } map &&
                       GetNetEntity(map) == _arena;                              // hide the roster after leaving this arena
    }

    public override void Shutdown()
    {
        _root?.Orphan();
        _root = null;
        _hud = null;
        base.Shutdown();
    }

    private sealed class RaidHudLayer(BossRaidHudControl hud) : LayoutContainer
    {
        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            hud.UpdateViewport(finalSize);

            hud.Measure(finalSize);
            return base.ArrangeOverride(finalSize);
        }
    }
}
