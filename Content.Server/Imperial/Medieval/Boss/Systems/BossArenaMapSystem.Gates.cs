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
    public bool TryEnsureArena(EntityUid gate, BossArenaGateComponent component)
    {
        if (component.ReturnToGate || component.ArenaMapPath == null)
            return false;

        var arenaId = component.ArenaId;
        var arenas = EntityQueryEnumerator<BossArenaMapComponent>();
        while (arenas.MoveNext(out var arenaUid, out var arena))
        {
            if (arena.ArenaId != arenaId || !ValidArena(arenaUid, arena) ||
                arena.EntryGate is { } owner && owner != gate && Exists(owner) && !Terminating(owner))
                continue;
            var mapId = Transform(arenaUid).MapID;                              // only reuse an unclaimed arena or this gate's one
            if (!_maps.IsInitialized(mapId))
                _maps.InitializeMap(mapId);
            _maps.SetPaused(mapId, false);
            ApplyAmbientLight(new(arenaUid, arena));
            return EntityManager.System<BossArenaGateSystem>().LinkGate(gate, arena.Entrance);
        }

        if (component.ArenaMapPath is not { } path ||
            !_loader.TryLoadMap(path, out var map, out _, new DeserializationOptions { InitializeMaps = true })) // load a separate map when no arena is free
            return false;
        if (!TryComp<BossArenaMapComponent>(map.Value.Owner, out var loaded) ||
            loaded.ArenaId != arenaId || !ValidArena(map.Value.Owner, loaded))
        {
            _maps.DeleteMap(map.Value.Comp.MapId);
            return false;
        }
        _maps.SetPaused(map.Value.Comp.MapId, false);
        return EntityManager.System<BossArenaGateSystem>().LinkGate(gate, loaded.Entrance);
    }

    public void BindGate(EntityUid gate, EntityUid entrance)
    {
        if (Transform(entrance).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena) ||
            arena.Entrance != entrance || Transform(gate).MapUid == map)
            return;
        if (arena.EntryGate == null || !Exists(arena.EntryGate.Value) || Terminating(arena.EntryGate.Value))
            arena.EntryGate = gate;                                              // claim this arena for this gate
        EntityManager.System<BossEncounterSystem>().AttachGate(arena.Boss, gate);
        if (arena.VictoryExit is { } exit && Exists(exit) && TryComp<BossArenaGateComponent>(exit, out var exitGate))
        {
            exitGate.Destination = arena.EntryGate;
            exitGate.Revision++;
            EntityManager.System<BossArenaGateSystem>().SetOpen(exit, true);
        }
    }

    internal void CreateVictoryExit(Entity<BossArenaMapComponent> arenaEntity)
    {
        var arena = arenaEntity.Comp;
        arena.VictoryExitPrototype ??= EntityManager.System<BossArenaContentSystem>().Resource(arena.ArenaId, "exit");
        if (arena.VictoryExit is { } previous && Exists(previous) && !Terminating(previous))
            return;

        if (arena.VictoryExitPrototype is not { } exitPrototype) return;
        var tile = arena.VictoryExitTile;
        var exit = Spawn(exitPrototype, new EntityCoordinates(arena.CombatGrid, tile.X + 0.5f, tile.Y + 0.5f));
        arena.VictoryExit = exit;
        var timer = Comp<BossArenaExitTimerComponent>(exit);
        timer.EndTime = arena.VictoryDeadline ?? _timing.CurTime + arena.VictoryLootDuration; // show the remaining loot time
        Dirty(exit, timer);
        var exitGate = Comp<BossArenaGateComponent>(exit);
        exitGate.Destination = arena.EntryGate;
        var gates = EntityManager.System<BossArenaGateSystem>();
        var portals = EntityQueryEnumerator<BossArenaGateComponent>();
        while (portals.MoveNext(out var entry, out var portal))
            if (!portal.ReturnToGate && portal.Destination == arena.Entrance)
                gates.SetOpen(entry, true);
        if (arena.EntryGate is { } fallback && Exists(fallback) && !Terminating(fallback))
            gates.SetOpen(exit, true);
    }
}
