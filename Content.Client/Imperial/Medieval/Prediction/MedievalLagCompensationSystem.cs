using Content.Shared.Imperial.Medieval.Prediction;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Timing;
using ClientMeleeWeaponSystem = Content.Client.Weapons.Melee.MeleeWeaponSystem;

namespace Content.Client.Imperial.Medieval.Prediction;

/// <summary>
/// Reports the client's processed server tick before melee input is generated.
/// The server uses it for the existing historical transform lookup.
/// </summary>
public sealed class MedievalLagCompensationSystem : EntitySystem
{
    [Dependency] private readonly IClientGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private GameTick _lastSentTick;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesBefore.Add(typeof(ClientMeleeWeaponSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sample once during fresh simulation, never during rollback replay.
        if (!_timing.InSimulation || !_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalSession == null || _player.LocalEntity == null)
        {
            _lastSentTick = GameTick.Zero;
            return;
        }

        var tick = _timing.LastRealTick;

        if (tick == GameTick.Zero || tick == _lastSentTick)
            return;

        // Use the predictive-event sequence so this report is ordered before
        // subsequent melee inputs from the same client simulation tick.
        RaisePredictiveEvent(new MedievalSetLastRealTickEvent(tick));
        _lastSentTick = tick;
    }
}
