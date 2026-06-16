using System;
using System.Threading;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Repairing;
using Content.Shared.Imperial.Medieval.Ships.WaterPump.Bucket;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

/// <summary>
/// This handles...
/// </summary>
public sealed class WaterPumpSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedWaterOnShipSystem _waterOnShip = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<WaterPumpComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WaterPumpComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<WaterPumpComponent, PumpUseEvent>(OnBucketUse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WaterPumpComponent>();
        while (query.MoveNext(out var pumpUid, out var pump))
            if (pump.UsedTime + TimeSpan.FromSeconds(1.25f) < _timing.CurTime && _timing.CurTime < pump.UsedTime + TimeSpan.FromSeconds(2f))
                _appearance.SetData(pumpUid, PumpVisuals.State, PumpState.Idle);
    }

    private void OnAfterInteract(EntityUid uid, WaterPumpComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;
        Use(args.User, uid, component);
    }

    private void OnActivateInWorld(EntityUid uid, WaterPumpComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        Use(args.User, uid, component);
    }

    private void Use(EntityUid playerEntity, EntityUid used, WaterPumpComponent component)
    {
        if (component.User is { } user && user != playerEntity)
            return;
        else if (component.User == playerEntity)
        {
            _doAfter.Cancel(component.DoAfter);
            return;
        }

        var query = EntityQueryEnumerator<WaterPumpComponent>();
        while (query.MoveNext(out _, out var pump))
            if (pump.User == playerEntity)
                return;

        var boat = _transform.GetParentUid(used);

        TryComp<MapGridComponent>(boat, out var boatComponent);
        if (boatComponent == null)
            return;

        var time = 7 - _skills.GetSkillLevel(playerEntity, "Agility") * 0.15f - _skills.GetSkillLevel(playerEntity, "Strength") * 0.25f;
        time = Math.Max(1.5f, time);
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new PumpUseEvent(),
            used,
            boat,
            used)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(sdoAfter, out var doAfterId))
        {
            component.User = playerEntity;
            component.DoAfter = doAfterId;
        }
        else
            component.User = null;
    }

    private void OnBucketUse(EntityUid uid, WaterPumpComponent component, PumpUseEvent args)
    {
        if (args.Cancelled || args.Target is null || args.Handled)
        {
            component.User = null;
            component.DoAfter = null;
            return;
        }

        _waterOnShip.RemoveWater(args.Target.Value, component.WaterCount);
        var audioParams = new Robust.Shared.Audio.AudioParams
        {
            Variation = 0.15f,
            Volume = -10f
        };
        _audio.PlayPredicted(MedievalShipSounds.PumpUse, uid, args.User, audioParams);
        _appearance.SetData(uid, PumpVisuals.State, PumpState.Active);
        component.UsedTime = _timing.CurTime;
        args.Repeat = true;
        args.Handled = true;
    }
}

[NetSerializable, Serializable]
public enum PumpVisuals : byte
{
    Layer,
    State
}

[NetSerializable, Serializable]
public enum PumpState : byte
{
    Idle,
    Active
}
