using Content.Shared.Hands.EntitySystems;
using Content.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.UseDelayAroundCursor;

public sealed class UseDelayAroundCursorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        var overlay = new UseDelayAroundCursorOverlay(
            _timing,
            _input,
            EntityManager,
            _player,
            _hands,
            _useDelay
        );

        _overlayMan.AddOverlay(overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<UseDelayAroundCursorOverlay>();
    }
}
