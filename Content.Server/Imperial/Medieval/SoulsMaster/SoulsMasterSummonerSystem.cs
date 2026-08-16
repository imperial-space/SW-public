/// <summary>
/// Spellward International add;
/// Periodically spawns configured summon entities around living mobs that have a
/// SoulsMasterSummonerComponent. The first summon is delayed by the
/// component's initial delay, and following summons use its configured cooldown
/// Also, Summoning stops when aggro is lost and resumes with the initial delay when
/// a new target is acquired. Each summoner tracks its own living summons and respects its
/// configured maximum in the YAML
/// </summary>
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
    // Run the main summoner query once every second (30 ticks)
    private const float UpdateInterval = 1f;

    // Stores time until the next summoner update
    private float _updateAccumulator;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Starts the summon timer when the summoner enters the map
        SubscribeLocalEvent<SoulsMasterSummonerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(
        EntityUid uid,
        SoulsMasterSummonerComponent component,
        MapInitEvent args)
    {
        // This timer is reset again when the summoner first becomes aggroed
        component.NextSummon = _timing.CurTime + component.InitialDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update() is called every tick, but the main logic only runs
        // after UpdateInterval seconds have elapsed
        _updateAccumulator += frameTime;

        if (_updateAccumulator < UpdateInterval)
            return;

        // Preserve any remaining time without running several
        // catch-up updates after a server stall
        _updateAccumulator %= UpdateInterval;

        // Query only the custom component
        // Other required components are retrieved below using TryComp<>
        var query = EntityQueryEnumerator<SoulsMasterSummonerComponent>();

        while (query.MoveNext(out var uid, out var summoner))
        {
            // Remove dead or deleted summons before calculating available slots
            PruneSummons(summoner);

            // Dead summoners cannot summon
            if (!TryComp<MobStateComponent>(uid, out var mobState) ||
                mobState.CurrentState != MobState.Alive)
            {
                summoner.WasAggroed = false;
                continue;
            }

            // The summoner requires a transform for spawning and an HTN
            // component for checking its current combat target
            if (!TryComp<TransformComponent>(uid, out var transform) ||
                !TryComp<HTNComponent>(uid, out var htn))
            {
                summoner.WasAggroed = false;
                continue;
            }

            // The HTN Target blackboard value represents the mob's active target
            var aggroed =
                htn.Blackboard.TryGetValue<EntityUid>(
                    "Target",
                    out var target,
                    EntityManager) &&
                EntityManager.EntityExists(target);

            // Stop summon processing while no active target exists
            if (!aggroed)
            {
                summoner.WasAggroed = false;
                continue;
            }

            // Start a fresh initial delay whenever the summoner acquires
            // a target after previously being unaggroed
            if (!summoner.WasAggroed)
            {
                summoner.WasAggroed = true;
                summoner.NextSummon =
                    _timing.CurTime + summoner.InitialDelay;

                continue;
            }

            // Wait until the initial delay or normal cooldown has elapsed
            if (summoner.NextSummon > _timing.CurTime)
                continue;

            // Begin the next cooldown before attempting to summon
            summoner.NextSummon =
                _timing.CurTime + summoner.Cooldown;

            // Never exceed the configured maximum number of active summons
            var availableSlots = Math.Max(
                0,
                summoner.MaxSummons - summoner.ActiveSummons.Count);

            // SummonCount controls the number spawned per cycle, while
            // availableSlots prevents the configured cap from being exceeded
            var amountToSpawn = Math.Min(
                Math.Max(0, summoner.SummonCount),
                availableSlots);

            for (var i = 0; i < amountToSpawn; i++)
            {
                // Search for a valid floor tile that is not blocked by
                // walls, impassable objects, or another mob
                if (!TryFindSpawnCoordinates(
                        transform.Coordinates,
                        summoner,
                        out var spawnCoordinates))
                {
                    continue;
                }

                // Play the configured visual effect at the spawned position
                Spawn(summoner.SummonEffect, spawnCoordinates);

                // Spawn and track the summon so it counts toward maxSummons
                var summon = Spawn(
                    summoner.SummonPrototype,
                    spawnCoordinates);

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

        // Summons must be placed on an actual map grid
        if (origin.GetGridUid(EntityManager) is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        // Try several random positions before giving up on this summon
        for (var attempt = 0;
             attempt < summoner.SpawnAttempts;
             attempt++)
        {
            var offset =
                _random.NextVector2() * summoner.SummonRadius;

            // Snap the random position to the center of tile
            var candidate =
                origin.Offset(offset).SnapToGrid(grid);

            // Reject missing tiles, space, walls, impassable objects,
            // and tiles currently occupied by mobs
            if (!_map.TryGetTileRef(
                    gridUid,
                    grid,
                    candidate,
                    out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                _turf.IsSpace(tileRef) ||
                _turf.IsTileBlocked(
                    tileRef,
                    CollisionGroup.Impassable |
                    CollisionGroup.MobMask))
            {
                continue;
            }

            spawnCoordinates = candidate;
            return true;
        }

        // No valid position found within the spawnAttempt count
        return false;
    }

    private void PruneSummons(
        SoulsMasterSummonerComponent summoner)
    {
        // Deleted or dead summons no longer occupy a summon slot
        summoner.ActiveSummons.RemoveWhere(summon =>
        {
            if (!EntityManager.EntityExists(summon))
                return true;

            return TryComp<MobStateComponent>(
                       summon,
                       out var mobState) &&
                   mobState.CurrentState != MobState.Alive;
        });
    }
}
