/// <summary>
/// Spellward International add;
/// Periodically spawns configured summon entities around living mobs that have a
/// SoulsMasterSummonerComponent. The first summon is delayed by the
/// component's initial delay, and following summons use its configured cooldown
/// Also, Summoning stops when aggro is lost and resumes with the initial delay when
/// a new target is acquired. Each summoner tracks its own living summons and respects its
/// configured maximum in the YAML
/// </summary>
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.SoulsMaster;

public sealed class SoulsMasterSummonerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SoulsMasterSummonerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SoulsMasterSummonerComponent component, MapInitEvent args)
    {
        component.NextSummon = _timing.CurTime + component.InitialDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SoulsMasterSummonerComponent, MobStateComponent, TransformComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var summoner, out var mobState, out var transform, out var htn))
        {
            PruneSummons(summoner);

            if (mobState.CurrentState != MobState.Alive)
                continue;

            var aggroed = htn.Blackboard.TryGetValue<EntityUid>("Target", out var target, EntityManager) &&
                          EntityManager.EntityExists(target);

            if (!aggroed)
            {
                summoner.WasAggroed = false;
                continue;
            }

            if (!summoner.WasAggroed)
            {
                summoner.WasAggroed = true;
                summoner.NextSummon = _timing.CurTime + summoner.InitialDelay;
                continue;
            }

            if (summoner.NextSummon > _timing.CurTime)
                continue;

            summoner.NextSummon = _timing.CurTime + summoner.Cooldown;

            var availableSlots = Math.Max(0, summoner.MaxSummons - summoner.ActiveSummons.Count);
            var amountToSpawn = Math.Min(Math.Max(0, summoner.SummonCount), availableSlots);

            for (var i = 0; i < amountToSpawn; i++)
            {
                if (!TryFindSpawnCoordinates(transform.Coordinates, summoner, out var spawnCoordinates))
                    continue;

                Spawn(summoner.SummonEffect, spawnCoordinates);

                var summon = Spawn(summoner.SummonPrototype, spawnCoordinates);
                summoner.ActiveSummons.Add(summon);
            }
        }
    }

    private bool TryFindSpawnCoordinates(
        EntityCoordinates origin,
        SoulsMasterSummonerComponent summoner,
        out EntityCoordinates spawnCoordinates)
    {
        spawnCoordinates = default;

        if (origin.GetGridUid(EntityManager) is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        for (var attempt = 0; attempt < summoner.SpawnAttempts; attempt++)
        {
            var offset = _random.NextVector2() * summoner.SummonRadius;
            var candidate = origin.Offset(offset).SnapToGrid(grid);

            if (!_map.TryGetTileRef(gridUid, grid, candidate, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                _turf.IsSpace(tileRef) ||
                _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable | CollisionGroup.MobMask))
            {
                continue;
            }

            spawnCoordinates = candidate;
            return true;
        }

        return false;
    }

    private void PruneSummons(SoulsMasterSummonerComponent summoner)
    {
        summoner.ActiveSummons.RemoveWhere(uid =>
            !EntityManager.EntityExists(uid) ||
            TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive);
    }
}
