using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chasm;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Mobs.Systems;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossArenaMapSystem
{
    internal void InitializeFalls()
    {
        SubscribeLocalEvent<BossArenaFallEvent>(OnStartFall);
        SubscribeLocalEvent<BossArenaFallingComponent, ComponentShutdown>(OnFallShutdown);
        SubscribeLocalEvent<BossArenaFallingComponent, ChasmFallCompleteEvent>(OnFallComplete);
        SubscribeLocalEvent<BossArenaFallingComponent, MoveEvent>(OnFallMove);
    }

    private void OnStartFall(ref BossArenaFallEvent args)
        => EnsureComp<BossArenaFallingComponent>(args.Player).Arena = args.Arena;

    internal void OnFallPlayerChanged(ref BossArenaPlayerEvent args)
    {
        if (args.Change == BossArenaPlayerChange.StopFalling) RemComp<BossArenaFallingComponent>(args.Player);
    }

    private void OnFallMove(EntityUid uid, BossArenaFallingComponent component, ref MoveEvent args)
    {
        if (Transform(uid).MapUid != component.Arena) CancelFall(uid);           // stop handling the fall if the body changes maps
    }

    private void OnFallComplete(EntityUid uid, BossArenaFallingComponent component, ref ChasmFallCompleteEvent args)
    {
        args.Handled = true;                                                    // keep the body instead of letting the chasm delete it
        if (!TryComp<BossArenaMapComponent>(component.Arena, out var arena) ||
            Transform(uid).MapUid != component.Arena || !FinishPitReturn(uid, component.Arena, arena))
            CancelFall(uid);
    }

    private void OnFallShutdown(EntityUid user, BossArenaFallingComponent component, ComponentShutdown args)
    {
        if (Terminating(user)) return;
        RemComp<ChasmFallingComponent>(user);
        _blocker.UpdateCanMove(user);
    }

    private bool FinishPitReturn(EntityUid user, EntityUid map, BossArenaMapComponent arena)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable) ||
            !_thresholds.TryGetDeadThreshold(user, out var threshold) || threshold.Value < 1 ||
            !TryComp<BossArenaEntranceComponent>(arena.Entrance, out var entrance) ||
            Transform(arena.Entrance).MapUid != map) return false;
        var gates = EntityManager.System<BossArenaTransportSystem>();
        var destination = _transform.ToCoordinates(arena.CombatGrid,
            _transform.ToMapCoordinates(new EntityCoordinates(arena.Entrance, entrance.ArrivalOffset)));
        CancelFall(user);
        gates.TransportUser(user, destination);                                 // same body and gear, back at the arena entrance
        var damage = new DamageSpecifier(damageable.Damage);
        damage.DamageDict["Blunt"] = damage.DamageDict.GetValueOrDefault("Blunt") +
            FixedPoint2.Max(FixedPoint2.Zero, threshold.Value + FixedPoint2.New(1) - damageable.TotalDamage);
        _damage.SetDamage(user, damageable, damage);
        _mobState.ChangeMobState(user, Content.Shared.Mobs.MobState.Dead);       // leave a dead body at the entrance
        return true;
    }
}
