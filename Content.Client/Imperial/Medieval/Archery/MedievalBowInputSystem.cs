
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Archery;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Medieval.Archery;

public sealed class MedievalBowInputSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private bool _wasSecondaryDown;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var secondaryDown =
            _inputSystem.CmdStates.GetState(
                EngineKeyFunctions.UseSecondary) == BoundKeyState.Down;

        var justPressed = secondaryDown && !_wasSecondaryDown;
        var justReleased = !secondaryDown && _wasSecondaryDown;

        _wasSecondaryDown = secondaryDown;

        if (_player.LocalEntity is not { } user ||
            !_combatMode.IsInCombatMode(user))
            return;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) ||
            activeItem is not { } bow ||
            !HasComp<MedievalBowChargeComponent>(bow))
            return;

        if (justPressed)
        {
            RaisePredictiveEvent(
                new MedievalBowChargeStart(GetNetEntity(bow)));
        }

        else if (justReleased)
        {
            var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

            if (mousePos.MapId == MapId.Nullspace)
            {
                RaisePredictiveEvent(new MedievalBowChargeCancel(GetNetEntity(bow)));
                return;
            }

            var coordinates = _transformSystem.ToCoordinates(user, mousePos);

            RaisePredictiveEvent(new MedievalBowChargeRelease(GetNetEntity(bow), GetNetCoordinates(coordinates), _timing.CurTick));
        }

    }
}
