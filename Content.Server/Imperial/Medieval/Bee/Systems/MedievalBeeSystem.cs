using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Microsoft.CodeAnalysis;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed class MedievalBeeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalBeeHiveComponent, ComponentInit>(HiveInitialize);
        SubscribeLocalEvent<MedievalBeeHiveComponent, InteractHandEvent>(HiveInteract);
        SubscribeLocalEvent<MedievalBeeSmokeComponent, BeforeRangedInteractEvent>(SmokeInteract);
        SubscribeLocalEvent<MedievalBeePlayerSpawnComponent, InteractHandEvent>(ExitInteract);
        SubscribeLocalEvent<MedievalBeePlayerSpawnComponent, MapInitEvent>(SpawnInitialize);
        SubscribeLocalEvent<MedievalBeeComponent, MapInitEvent>(BeeInitialize);
        SubscribeLocalEvent<MedievalBeeComponent, DamageModifyEvent>(BeeDamaged);
        SubscribeLocalEvent<MedievalBeeSmokeComponent, ExaminedEvent>(SmokeExamined);
        SubscribeLocalEvent<MedievalBeeHiveComponent, ExaminedEvent>(HiveExamined);
        SubscribeLocalEvent<MedievalBeeTrapComponent, StartCollideEvent>(TrapCollide);
        SubscribeLocalEvent<MedievalBeeTrappedComponent, InteractHandEvent>(TrappedInteract);
        SubscribeLocalEvent<MedievalBeeItemSourceComponent, InteractHandEvent>(ItemSourceInteract);
        SubscribeLocalEvent<MedievalBeeItemSourceComponent, InteractUsingEvent>(ItemSourceInteractUsing);
        SubscribeLocalEvent<MedievalBeeLinkedSpawnerComponent, MapInitEvent>(SpawnerInit);
        SubscribeLocalEvent<MedievalBeeLinkedMobComponent, MobStateChangedEvent>(LinkedMobStateChanged);
        SubscribeLocalEvent<MedievalBeeChanceSpawnComponent, MapInitEvent>(ChanceSpawnInit);
        SubscribeLocalEvent<MedievalBeeHiveComponent, DestructionEventArgs>(HiveDestroyed);
    }
    public void Teleport(EntityUid uid, EntityUid target)
    {
        if (TryComp<PullerComponent>(uid, out var pullerComp))
        {
            if (pullerComp != null && pullerComp.Pulling != null)
            {
                if (!TryComp<PullableComponent>(pullerComp.Pulling, out var pullableComp1)) return;
                _pulling.TryStopPull(pullerComp.Pulling.Value, pullableComp1);
                return;
            }
        }
        if (TryComp<PullableComponent>(uid, out var pullableComp))
        {
            if (pullableComp != null && pullableComp.Puller != null)
            {
                if (!TryComp<PullerComponent>(pullableComp.Puller, out var pullerComp1)) return;
                _pulling.TryStopPull(pullableComp.Puller.Value, pullableComp);
                return;
            }
        }
        _transform.SetCoordinates(uid, Transform(target).Coordinates);
        _transform.AttachToGridOrMap(uid);
    }
    public bool TryGetHiveGridFromTransform(EntityUid uid, [NotNullWhen(true)] out Entity<MedievalBeeGridComponent>? result)
    {
        result = null;
        var grid = _transform.GetGrid(uid);

        if (grid == null)
            return false;

        if (!TryComp<MedievalBeeGridComponent>(grid, out var gridComponent) || !gridComponent.Hive.HasValue)
            return false;

        result = new(grid.Value, gridComponent);
        return true;
    }
    public void Pacify(Entity<MedievalBeeHiveComponent> hive, TimeSpan time)
    {
        foreach (var entity in hive.Comp.Bees)
        {
            Pacify(entity.Owner, entity.Comp);
        }
        hive.Comp.Pacified = true;
        hive.Comp.PacifyEnd = _timing.CurTime + time;
        hive.Comp.PacifyCooldown = _timing.CurTime + (time * 2);
    }
    public void UnPacify(Entity<MedievalBeeHiveComponent> hive)
    {
        foreach (var entity in hive.Comp.Bees)
        {
            UnPacify(entity.Owner, entity.Comp);
        }
        hive.Comp.Pacified = false;
    }
    public void UnPacify(EntityUid uid, MedievalBeeComponent? component)
    {
        if (Deleted(uid))
            return;

        if (!Resolve(uid, ref component))
            return;

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factionComponent))
            return;

        _faction.ClearFactions((uid, factionComponent));
        _faction.AddFaction((uid, factionComponent), component.HostileFaction);
    }
    public void Pacify(EntityUid uid, MedievalBeeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!TryComp<NpcFactionMemberComponent>(uid, out var factionComponent))
            return;

        _faction.ClearFactions((uid, factionComponent));
        _faction.AddFaction((uid, factionComponent), component.FriendlyFaction);
    }
    private bool TrySpawnItemFromSource(Entity<MedievalBeeItemSourceComponent> ent, EntityUid target, out EntityUid? result)
    {
        result = null;
        if (ent.Comp.NextGather > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-itemsource-cooldown"), target, target);
            return false;
        }
        ent.Comp.NextGather = _timing.CurTime + ent.Comp.GatherCooldown;
        var item = Spawn(ent.Comp.Item, Transform(target).Coordinates);
        _stack.TryMergeToHands(item, target);
        _popup.PopupEntity(Loc.GetString("medieval-bee-itemsource-succesful"), target, target);
        result = item;
        return true;
    }
    private void ItemSourceInteractUsing(Entity<MedievalBeeItemSourceComponent> ent, ref InteractUsingEvent args)
    {
        TrySpawnItemFromSource(ent, args.User, out _);
    }
    private void ItemSourceInteract(Entity<MedievalBeeItemSourceComponent> ent, ref InteractHandEvent args)
    {
        TrySpawnItemFromSource(ent, args.User, out _);
    }
    private void TrappedInteract(EntityUid uid, MedievalBeeTrappedComponent component, InteractHandEvent args)
    {
        if (args.User == uid)
            return;

        if (!TryComp<KnockedDownComponent>(uid, out var stunned))
        {
            RemComp<MedievalBeeTrappedComponent>(uid);
            return;
        }
        //if (!HasComp<HumanoidAppearanceComponent>(args.OtherEntity))
        //    return;

        _stun.SetKnockdownTime((uid, stunned), TimeSpan.Zero);
        RemComp<MedievalBeeTrappedComponent>(uid);
    }
    private void TrapCollide(EntityUid uid, MedievalBeeTrapComponent component, StartCollideEvent args)
    {
        if (component.CooldownEnd.HasValue && component.CooldownEnd > _timing.CurTime)
            return;

        if (HasComp<MedievalBeeTrappedComponent>(args.OtherEntity))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(args.OtherEntity))
            return;

        if (!_stun.TryKnockdown(args.OtherEntity, component.StunTime, force: true))
            return;

        var comp = AddComp<MedievalBeeTrappedComponent>(args.OtherEntity);
        comp.RemoveTime = _timing.CurTime + component.StunTime;
        component.CooldownEnd = _timing.CurTime + component.Cooldown;
    }
    private void HiveExamined(EntityUid uid, MedievalBeeHiveComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"medieval-bee-pacified-{component.Pacified.ToString().ToLower()}"));
        if (component.PacifyEnd.HasValue)
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-yes", ("time", (_timing.CurTime - component.PacifyEnd.Value).ToString())));
        }
        else
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-no"));
        }
    }
    private void SmokeExamined(EntityUid uid, MedievalBeeSmokeComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("medieval-bee-smoke-uses-left", ("uses", component.UsesLeft.ToString())));
    }
    private void BeeDamaged(EntityUid uid, MedievalBeeComponent component, DamageModifyEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        if (!component.ConnectedHive.HasValue)
            return;

        if (!component.ConnectedHive.Value.Comp.Pacified)
            return;

        UnPacify(component.ConnectedHive.Value);
    }
    private void ExitInteract(EntityUid uid, MedievalBeePlayerSpawnComponent component, InteractHandEvent args)
    {
        Teleport(args.User, component.Hive);
    }
    private void HiveInitialize(EntityUid uid, MedievalBeeHiveComponent component, ComponentInit args)
    {
        if (!_mapLoader.TryLoadMap(new(_random.Pick(_proto.Index(component.GridDataset).Values)), out var createdMap, out var grids))
        {
            Log.Error("failure while initializing bee hive");
            return;
        }
        if (grids.Count > 1)
        {
            Log.Warning("loading bee hive with multiple grids, may cause issues");
        }
        var grid = grids.First();
        var gridComponent = EnsureComp<MedievalBeeGridComponent>(grid);
        component.Grid = (grid, gridComponent);
        gridComponent.Hive = uid;
        _mapSystem.InitializeMap((createdMap.Value.Owner, createdMap.Value.Comp));
    }
    private void SpawnInitialize(Entity<MedievalBeePlayerSpawnComponent> spawn, ref MapInitEvent args)
    {
        if (!TryGetHiveGridFromTransform(spawn, out var grid))
            return;

        if (!grid.Value.Comp.Hive.HasValue)
        {
            Log.Warning("player spawn spawned on invalid grid, despawning");
            QueueDel(spawn);
            return;
        }

        grid.Value.Comp.Spawns.Add(spawn);
        spawn.Comp.Hive = grid.Value.Comp.Hive.Value;
    }
    private void HiveInteract(EntityUid uid, MedievalBeeHiveComponent component, InteractHandEvent args)
    {
        if (component.Grid.Comp.Spawns.Count() <= 0)
        {
            return;
        }
        var spawn = _random.Pick(component.Grid.Comp.Spawns);
        Teleport(args.User, spawn.Owner);
        args.Handled = true;
    }
    private void SmokeInteract(EntityUid uid, MedievalBeeSmokeComponent component, BeforeRangedInteractEvent args)
    {
        if (!args.Target.HasValue)
            return;

        if (!TryComp<MedievalBeeHiveComponent>(args.Target.Value, out var hiveComponent))
            return;

        if (hiveComponent.Pacified)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-pacify-already"), args.User, args.User);
            return;
        }
        if (hiveComponent.PacifyCooldown.HasValue && hiveComponent.PacifyCooldown > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("medieval-bee-pacify-cooldown"), args.User, args.User);
            return;
        }
        _popup.PopupEntity(Loc.GetString("medieval-pacify-succesful"), args.User, args.User);
        Pacify((uid, hiveComponent), component.PacifyTime);
        component.UsesLeft--;
        if (component.UsesLeft <= 0)
            QueueDel(uid);

        args.Handled = true;
    }
    private void BeeInitialize(EntityUid uid, MedievalBeeComponent component, MapInitEvent args)
    {
        if (!TryGetHiveGridFromTransform(uid, out var grid))
            return;

        var hive = grid.Value.Comp.Hive;
        if (!TryComp<MedievalBeeHiveComponent>(hive, out var hiveComponent))
            return;

        component.ConnectedHive = (hive.Value, hiveComponent);
        hiveComponent.Bees.Add((uid, component));
        if (hiveComponent.Pacified)
        {
            Pacify(uid, component);
        }
    }
    private void SpawnerInit(EntityUid uid, MedievalBeeLinkedSpawnerComponent component, MapInitEvent args)
    {
        component.NextSpawn = _timing.CurTime;
    }
    private void LinkedMobStateChanged(EntityUid uid, MedievalBeeLinkedMobComponent component, MobStateChangedEvent args)
    {
        if (!component.LinkedSpawner.HasValue)
            return;

        if (component.LinkedSpawner.Value.Comp.NextSpawn.HasValue)
            return;

        if (args.NewMobState == MobState.Alive)
            return;

        component.LinkedSpawner.Value.Comp.NextSpawn = _timing.CurTime + component.LinkedSpawner.Value.Comp.RespawnTime;
    }
    private void ChanceSpawnInit(EntityUid uid, MedievalBeeChanceSpawnComponent component, MapInitEvent args)
    {
        if (!_random.Prob(component.Chance))
            return;

        if (!_random.TryPickAndTake(component.Entities, out var ent))
            return;

        Spawn(ent, Transform(uid).Coordinates);
    }
    private void HiveDestroyed(EntityUid uid, MedievalBeeHiveComponent component, DestructionEventArgs args)
    {
        HashSet<Entity<MobStateComponent>> mobs = new();
        _lookup.GetGridEntities(component.Grid, mobs);
        foreach (var mob in mobs)
        {
           Teleport(mob.Owner, uid);
        }
        QueueDel(Transform(component.Grid).MapUid);
    }
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private TimeSpan _updateCooldown = TimeSpan.FromSeconds(1);
    public override void Update(float frameTime)
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + _updateCooldown;
        var hiveQuery = EntityQueryEnumerator<MedievalBeeHiveComponent>();
        while (hiveQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.PacifyEnd > _timing.CurTime)
                continue;

            comp.PacifyEnd = null;
            if (comp.Pacified)
                UnPacify((uid, comp));

        }
        var trappedQuery = EntityQueryEnumerator<MedievalBeeTrappedComponent>();
        while (trappedQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.RemoveTime > _timing.CurTime)
                continue;

            RemComp<MedievalBeeTrappedComponent>(uid);
        }
        var spawnerQuery = EntityQueryEnumerator<MedievalBeeLinkedSpawnerComponent>();
        while (spawnerQuery.MoveNext(out var uid, out var comp))
        {
            if (!comp.NextSpawn.HasValue || comp.NextSpawn > _timing.CurTime)
                continue;

            if (comp.LinkedEntity.HasValue && !Deleted(comp.LinkedEntity) && (!TryComp<MobStateComponent>(comp.LinkedEntity.Value, out var state) || state.CurrentState == MobState.Alive))
                continue;

            var mob = _random.Pick(comp.Mobs);
            var createdMob = Spawn(mob, Transform(uid).Coordinates);
            QueueDel(comp.LinkedEntity);
            var mobComp = EnsureComp<MedievalBeeLinkedMobComponent>(createdMob);
            mobComp.LinkedSpawner = (uid, comp);
            comp.LinkedEntity = createdMob;
            comp.NextSpawn = null;
        }
    }
}
