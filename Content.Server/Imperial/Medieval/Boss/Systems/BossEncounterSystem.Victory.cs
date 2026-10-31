using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Medieval.Boss;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossEncounterSystem
{
    private void OnDefeated(ref BossDefeatedEvent args)
    {
        if (!TryComp<BossEncounterComponent>(args.Boss, out var comp) || !_telegraphs.TryGetArena(args.Boss, out var arena)) return;
        arena.Comp.VictoryDeadline = _timing.CurTime + arena.Comp.VictoryLootDuration;
        arena.Comp.NextVictoryReturn = _timing.CurTime;
        arena.Comp.VictoryWarningSent = false;
        var victory = new BossArenaStateEvent(arena, BossArenaState.Victory);
        RaiseLocalEvent(ref victory);
        var gates = EntityManager.System<BossArenaTransportSystem>();
        foreach (var player in comp.Party.Where(player => Exists(player) && !Terminating(player) &&
                     Transform(player).MapUid == arena.Owner && !_mobs.IsAlive(player)).ToArray())
        {
            if (!_telegraphs.TryGetPlatformTile(args.Boss, player, out _))
            {
                if (!gates.TryGetPlatformRecoveryPosition(player, arena.Comp.CombatGrid, out var landing)) continue;
                gates.TransportUser(player, landing);
            }
            Revive(player);
        }
        foreach (var player in VictoryOccupants(arena))
            _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Loot_Time",
                ("minutes", Math.Ceiling(arena.Comp.VictoryLootDuration.TotalMinutes))), player, player, PopupType.Medium);
    }

    public void Revive(EntityUid player)
    {
        EntityManager.System<BossArenaMapSystem>().CancelFall(player);
        if (TryComp<DoAfterComponent>(player, out var actions))
            foreach (var id in actions.DoAfters.Keys.ToArray()) _actions.Cancel(player, id, actions);
        _rejuvenate.PerformRejuvenate(player);
        if (_thresholds.TryGetIncapThreshold(player, out var threshold) && TryComp<DamageableComponent>(player, out var damageable))
        {
            var injury = new DamageSpecifier(damageable.Damage);
            injury.DamageDict["Blunt"] = FixedPoint2.Max(FixedPoint2.Zero, threshold.Value - FixedPoint2.New(BossArenaMapSystem.RecoveryHealth));
            _damage.SetDamage(player, damageable, injury);
        }
        if (TryComp<BloodstreamComponent>(player, out var blood))
        {
            _blood.TryModifyBloodLevel((player, blood), blood.BloodMaxVolume);
            _blood.TryModifyBleedAmount((player, blood), -blood.BleedAmount);
        }
        _mobs.ChangeMobState(player, MobState.Alive);
        if (_minds.TryGetMind(player, out var mind, out var mindComp))
        {
            _minds.UnVisit(mind, mindComp);
            _minds.TransferTo(mind, player, mind: mindComp);
        }
    }

    private EntityUid[] VictoryOccupants(Entity<BossArenaMapComponent> arena)
        => EntityManager.AllEntities<MobStateComponent>()
            .Where(player => !Terminating(player.Owner) && _rules.IsPartyBody(player.Owner) && Transform(player.Owner).MapUid == arena.Owner)
            .Select(player => player.Owner).ToArray();



    private void CloseEntryGates(Entity<BossArenaMapComponent> arena)
    {
        var state = new BossArenaStateEvent(arena, BossArenaState.Closed);
        RaiseLocalEvent(ref state);
    }

    private void CompleteVictory(Entity<BossArenaMapComponent> arena, EntityUid[] occupants)
    {

        CloseEntryGates(arena);
        var gates = EntityManager.System<BossArenaTransportSystem>();
        var returns = new Dictionary<EntityUid, EntityCoordinates>();
        foreach (var player in occupants)
        {
            if (!gates.TryReturnPosition(player, arena, out var destination, fallbackOwner: true)) return;
            returns[player] = destination;
        }
        foreach (var (player, destination) in returns)
        {
            gates.TransportUser(player, destination);
            if (!_mobs.IsAlive(player)) Revive(player);
            EntityManager.System<BossArenaMapSystem>().LeaveArena(player, arena.Owner);
            _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Expired"), player, player, PopupType.Medium);
        }
        if (Exists(arena.Comp.Boss) && !Terminating(arena.Comp.Boss)) Del(arena.Comp.Boss);
        ClearVictoryItems(arena);
        SpawnFreshBoss(arena);
    }

    private void ClearVictoryItems(Entity<BossArenaMapComponent> arena)
    {
        var items = EntityManager.AllEntities<ItemComponent>()
            .Where(item => !Terminating(item.Owner) && Transform(item.Owner).MapUid == arena.Owner &&
                           !HasComp<MobStateComponent>(item.Owner))
            .Select(item => item.Owner).ToArray();
        foreach (var item in items)
            if (Exists(item) && !Terminating(item) && Transform(item).MapUid == arena.Owner)
                Del(item);
    }

    private void UpdateVictory()
    {
        foreach (var arena in EntityManager.AllEntities<BossArenaMapComponent>().ToArray())
        {
            if (arena.Comp.VictoryDeadline is not { } deadline || MetaData(arena.Owner).EntityPaused) continue;
            var occupants = VictoryOccupants(arena);
            if (!arena.Comp.VictoryWarningSent && arena.Comp.VictoryLootDuration > TimeSpan.FromMinutes(1) &&
                deadline - _timing.CurTime <= TimeSpan.FromMinutes(1))
            {
                arena.Comp.VictoryWarningSent = true;
                foreach (var player in occupants)
                    _popup.PopupEntity(Loc.GetString(arena.Comp.MessagePrefix + "Victory_Closing"), player, player, PopupType.MediumCaution);
            }
            if (occupants.Length == 0 || _timing.CurTime >= deadline && _timing.CurTime >= arena.Comp.NextVictoryReturn)
            {
                arena.Comp.NextVictoryReturn = _timing.CurTime + TimeSpan.FromSeconds(1);
                CompleteVictory(arena, occupants);
            }
        }

    }
}
