using Content.Server.Explosion.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.ShiftFront.Components;
using Content.Shared.Armable;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.LandMines;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Content.Server.Myrmex.Components; // imperial medieval

namespace Content.Server.LandMines;

public sealed class LandMineSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LandMineComponent, StepTriggeredOnEvent>(HandleStepOnTriggered);
        SubscribeLocalEvent<LandMineComponent, StepTriggeredOffEvent>(HandleStepOffTriggered);

        SubscribeLocalEvent<LandMineComponent, StepTriggerAttemptEvent>(HandleStepTriggerAttempt);
    }

    private void HandleStepOnTriggered(EntityUid uid, LandMineComponent component, ref StepTriggeredOnEvent args)
    {
        if (component.AntiTank && !HasComp<ShiftTankHullComponent>(args.Tripper)) return;
        _popupSystem.PopupCoordinates(
            Loc.GetString("land-mine-triggered", ("mine", uid)),
            Transform(uid).Coordinates,
            args.Tripper,
            PopupType.LargeCaution);

        _audioSystem.PlayPvs(component.Sound, uid);
        if (component.InstantTrigger)
            _trigger.Trigger(uid, args.Tripper);
    }

    private void HandleStepOffTriggered(EntityUid uid, LandMineComponent component, ref StepTriggeredOffEvent args)
    {
        if (component.AntiTank && !HasComp<ShiftTankHullComponent>(args.Tripper)) return;
        // TODO: Adjust to the new trigger system
        _trigger.Trigger(uid, args.Tripper, TriggerSystem.DefaultTriggerKey);
    }

    private static void HandleStepTriggerAttempt(EntityUid uid, LandMineComponent component, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;
    }
}
