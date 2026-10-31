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

public sealed partial class BossArenaMapSystem : EntitySystem
{
    public const int RecoveryHealth = 5;

    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedBloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChasmSystem _chasm = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BossDefeatedEvent>(OnBossDefeated);
        SubscribeLocalEvent<BossArenaMapComponent, MapInitEvent>(OnArenaInit);
    }

    private void OnArenaInit(EntityUid uid, BossArenaMapComponent component, MapInitEvent args)
    {
        EnsureComp<BossArenaRulesComponent>(uid);
        ApplyAmbientLight(new(uid, component));
        var loaded = new BossArenaStateEvent(new(uid, component), BossArenaState.Loaded);
        RaiseLocalEvent(ref loaded);
    }

    private void ApplyAmbientLight(Entity<BossArenaMapComponent> arena)
    {

        if (TryComp<BossMechanicComponent>(arena.Comp.Boss, out var mechanic) && mechanic.Blackout) return;
        _maps.SetAmbientLight(Transform(arena.Owner).MapID, arena.Comp.AmbientLight);
    }



    private bool ValidArena(EntityUid uid, BossArenaMapComponent arena)
        => Exists(uid) && !Terminating(uid) && Transform(uid).MapID != MapId.Nullspace &&
           arena.HalfSize is >= 2 and <= 32 && !string.IsNullOrWhiteSpace(arena.ArenaId) &&
           Exists(arena.Entrance) && !Terminating(arena.Entrance) &&
           HasComp<BossArenaEntranceComponent>(arena.Entrance) && Transform(arena.Entrance).MapUid == uid &&
           HasComp<MapGridComponent>(arena.CombatGrid) && Transform(arena.CombatGrid).MapUid == uid;



    public void RecordEntry(EntityUid user, EntityUid gate, EntityUid entrance)
    {
        if (Transform(entrance).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena))
            return;
        var joined = new BossArenaPlayerEvent(user, BossArenaPlayerChange.Joined, map, gate);
        RaiseLocalEvent(ref joined);
        EnsureComp<BossArenaPartyMemberComponent>(user);
        EntityManager.System<BossEncounterSystem>().Join(arena.Boss, user);
    }

    public List<EntityUid> GetParty(EntityUid map)
    {
        var result = new List<EntityUid>();
        var rules = EntityManager.System<SharedBossArenaRulesSystem>();
        var query = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
            if (xform.MapUid == map && !Terminating(uid) && rules.IsPartyBody(uid))
                result.Add(uid);
        result.Sort();
        return result;
    }

    public bool HasEntrySpace(EntityUid destination, EntityUid user)
    {
        if (Transform(destination).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena))
            return true;
        var party = GetParty(map);
        return party.Contains(user) || party.Count < arena.MaxPlayers;
    }

    public void LeaveArena(EntityUid user, EntityUid? arenaMap)
    {
        if (TryComp<FightingBossComponent>(user, out var fight) && fight.Boss is { } netBoss)
        {
            var resolved = TryGetEntity(netBoss, out var boss);
            if (!resolved || Transform(boss!.Value).MapUid == arenaMap)
            {
                if (resolved && TryComp<BossComponent>(boss, out var component))
                    component.Players.Remove(user);
                RemComp<FightingBossComponent>(user);
            }
        }
        var left = new BossArenaPlayerEvent(user, BossArenaPlayerChange.Left, arenaMap);
        RaiseLocalEvent(ref left);
        RemComp<BossArenaPartyMemberComponent>(user);
    }







    public void CancelFall(EntityUid user)
    {
        var request = new BossArenaPlayerEvent(user, BossArenaPlayerChange.StopFalling);
        RaiseLocalEvent(ref request);
    }









    public void SetCenterBlocked(Entity<BossArenaMapComponent> arena, bool blocked)
    {
        if (!Exists(arena.Comp.CombatGrid) || Terminating(arena.Comp.CombatGrid)) return;
        var blockers = EntityManager.AllEntities<BossArenaCenterBlockerComponent>()
            .Where(ent => !Terminating(ent.Owner) && Transform(ent.Owner).GridUid == arena.Comp.CombatGrid)
            .Select(ent => ent.Owner).ToArray();
        if (!blocked)
        {
            foreach (var blocker in blockers) Del(blocker);
        }
        else if (blockers.Length == 0 && arena.Comp.CenterBlockerPrototype is { } prototype)
            Spawn(prototype, new EntityCoordinates(arena.Comp.CombatGrid, 0.5f, 0.5f));
    }


    private void OnBossDefeated(ref BossDefeatedEvent args)
    {
        if (Transform(args.Boss).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena) ||
            !ValidArena(map, arena) || arena.Boss != args.Boss)
            return;
        SetCenterBlocked(new(map, arena), false);
    }
}
