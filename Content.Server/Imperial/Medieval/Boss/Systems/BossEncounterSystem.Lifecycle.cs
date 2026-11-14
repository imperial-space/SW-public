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
    public bool Reset(EntityUid boss, Entity<BossArenaMapComponent> arena)
    {
        if (!TryComp<BossEncounterComponent>(boss, out var comp)) return false;
        var gates = EntityManager.System<BossArenaTransportSystem>();
        var returns = new Dictionary<EntityUid, EntityCoordinates>();                              // find every return spot before moving anyone
        foreach (var player in comp.Party.Where(player => Exists(player) && !Terminating(player) && Transform(player).MapUid == arena.Owner))
        {
            if (!gates.TryReturnPosition(player, arena, out var destination)) return false;
            returns[player] = destination;
        }
        comp.Resetting = true;
        EntityManager.System<BossSystem>().CancelBossCast(boss);
        foreach (var (player, destination) in returns)
        {
            gates.TransportUser(player, destination);
            Revive(player);
            EntityManager.System<BossArenaMapSystem>().LeaveArena(player, arena.Owner);
        }
        End(boss);
        Comp<BossComponent>(boss).Active = false;
        EntityManager.System<BossSystem>().SendPlayersBack(boss, false);
        EntityManager.System<BossFocusMarkerSystem>().Clear(boss);
        Del(boss);
        SpawnFreshBoss(arena);
        Reopen(arena);
        return true;
    }

    public bool PrepareRetry(Entity<BossArenaMapComponent> arena)
        => arena.Comp.VictoryDeadline == null &&
           (arena.Comp.Crystal is { } crystal && Exists(crystal) && !Terminating(crystal) &&
            HasComp<BossSummoningTriggerComponent>(crystal) ||
            Exists(arena.Comp.Boss) && !Terminating(arena.Comp.Boss) &&
            TryComp<BossComponent>(arena.Comp.Boss, out var boss) && !boss.Finished);

    private void SpawnFreshBoss(Entity<BossArenaMapComponent> arena)
    {
        arena.Comp.VictoryDeadline = null;
        arena.Comp.NextVictoryReturn = TimeSpan.Zero;
        arena.Comp.VictoryWarningSent = false;
        EntityManager.System<BossArenaMapSystem>().SetCenterBlocked(arena, true);
        var reset = new BossArenaStateEvent(arena, BossArenaState.Reset);
        RaiseLocalEvent(ref reset);
        if (arena.Comp.VictoryExit is { } exit && Exists(exit)) Del(exit);
        arena.Comp.VictoryExit = null;
        foreach (var reward in EntityManager.AllEntities<BossEncounterRewardComponent>().Where(ent => Transform(ent.Owner).MapUid == arena.Owner).ToArray())
            Del(reward.Owner);
        EntityManager.System<BossSummoningTriggerSystem>().Restore(arena);
    }


    private void Reopen(Entity<BossArenaMapComponent> arena)
    {
        var state = new BossArenaStateEvent(arena, BossArenaState.Reopened);
        RaiseLocalEvent(ref state);
    }
}
