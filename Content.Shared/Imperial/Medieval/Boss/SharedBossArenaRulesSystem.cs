using System.Linq;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Pulling.Events;
using Content.Shared.Imperial.Medieval.Magic.TurnedToStone.Events;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Medieval.Magic;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class SharedBossArenaRulesSystem : EntitySystem
{
    [Dependency]
    private readonly SharedTransformSystem _transform = default!;
    [Dependency]
    private readonly IPrototypeManager _prototypes = default!;
    public readonly record struct SpellSource(EntityUid Player, EntityUid? Arena);
    public SpellSource? CurrentSource { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<BossArenaProtectedComponent, ContainerGettingInsertedAttemptEvent>(OnProtectedInsert);
        SubscribeLocalEvent<BossArenaPartyMemberComponent, ContainerGettingInsertedAttemptEvent>(OnPartyInsert);
        SubscribeLocalEvent<MobStateComponent, ContainerGettingInsertedAttemptEvent>(OnBodyInsert);
        SubscribeLocalEvent<EntityStorageComponent, ContainerIsInsertingAttemptEvent>(OnEntityStorageInsert);
        SubscribeLocalEvent<BossArenaProtectedComponent, BeingPulledAttemptEvent>(OnPull);
        SubscribeLocalEvent<BossArenaProtectedComponent, BeforeTurnetToStone>(OnPetrify);
        SubscribeLocalEvent<BossArenaSpellSourceComponent, PreventCollideEvent>(OnSpellCollision);
        SubscribeLocalEvent<BossArenaCenterBlockerComponent, PreventCollideEvent>(OnCenterCollision);
        SubscribeLocalEvent<MobStateComponent, RayCastSort>(OnMeleeRaycast);
        SubscribeLocalEvent<ItemComponent, ThrownEvent>(OnItemThrown);
        SubscribeLocalEvent<ItemComponent, UseInHandEvent>(OnItemUsed);
        SubscribeLocalEvent<ItemComponent, ActivateInWorldEvent>(OnItemActivated);
    }

    private void OnItemThrown(EntityUid uid, ItemComponent component, ref ThrownEvent args) => TagSpell(uid, args.User);
    private void OnItemUsed(EntityUid uid, ItemComponent component, UseInHandEvent args) => TagSpell(uid, args.User);
    private void OnItemActivated(EntityUid uid, ItemComponent component, ActivateInWorldEvent args) => TagSpell(uid, args.User);
    private void OnMeleeRaycast(EntityUid uid, MobStateComponent component, ref RayCastSort args)
    {
        if (!IsArena(uid))
            return;
        args.HitEntities.RemoveAll(hit => HasComp<BossArenaCenterBlockerComponent>(hit.HitEntity));
    }

    private void OnProtectedInsert(EntityUid uid, BossArenaProtectedComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (IsArena(uid))
            args.Cancel();
    }

    private void OnPartyInsert(EntityUid uid, BossArenaPartyMemberComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (!CanInsertBody(uid, args.Container))
            args.Cancel();
    }

    private void OnBodyInsert(EntityUid uid, MobStateComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (!CanInsertBody(uid, args.Container))
            args.Cancel();
    }

    private void OnEntityStorageInsert(EntityUid uid, EntityStorageComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID == SharedEntityStorageSystem.ContainerName && !CanUseEntityStorage(args.EntityUid, uid))
            args.Cancel();
    }

    private void OnPull(EntityUid uid, BossArenaProtectedComponent component, BeingPulledAttemptEvent args)
    {
        if (IsArena(uid))
            args.Cancel();
    }

    private void OnPetrify(EntityUid uid, BossArenaProtectedComponent component, BeforeTurnetToStone args)
    {
        if (IsArena(uid))
            args.Cancel();
    }

    private void OnSpellCollision(EntityUid uid, BossArenaSpellSourceComponent component, ref PreventCollideEvent args)
    {
        if (TryComp<ProjectileComponent>(uid, out var projectile) && !projectile.Damage.AnyPositive() && projectile.Damage.DamageDict.Values.Any(value => value < 0))
            return;
        if (BlocksFriendlyEffect(args.OtherEntity, uid))
            args.Cancelled = true;
    }

    private void OnCenterCollision(EntityUid uid, BossArenaCenterBlockerComponent component, ref PreventCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity) || HasComp<GhostComponent>(args.OtherEntity) || HasComp<BossArenaProtectedComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    public bool IsArena(EntityUid uid) => TryComp<TransformComponent>(uid, out var xform) && xform.MapUid is { } map && HasComp<BossArenaRulesComponent>(map);
    public bool PreservesArmor(EntityUid target, EntityUid? origin) => origin is { } source && HasComp<BossArenaAttackSourceComponent>(source) && IsArena(source) && IsArena(target) && Transform(source).MapUid == Transform(target).MapUid;
    public bool IsArena(EntityCoordinates coordinates) => IsArena(coordinates.EntityId);
    public bool IsArena(MapCoordinates coordinates)
    {
        var maps = EntityQueryEnumerator<BossArenaRulesComponent, TransformComponent>();
        while (maps.MoveNext(out _, out _, out var xform))
            if (xform.MapID == coordinates.MapId)
                return true;
        return false;
    }

    public bool IsPartyBody(EntityUid uid)
        => !HasComp<BossActorComponent>(uid) && !HasComp<BossArenaAttackSourceComponent>(uid) &&
           !HasComp<GhostComponent>(uid) &&
           (HasComp<BossArenaPartyMemberComponent>(uid) || HasComp<ActorComponent>(uid) ||
            TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind);
    public SpellSource? GetSource(EntityUid? uid)
    {
        for (var depth = 0; depth < 4 && uid != null; depth++)
        {
            if (HasComp<BossActorComponent>(uid) || HasComp<BossArenaAttackSourceComponent>(uid))
                return null;
            if (TryComp<BossArenaSpellSourceComponent>(uid, out var source))
                return IsArena(uid.Value) ? new SpellSource(source.Player, Transform(uid.Value).MapUid) : null;
            if (IsPartyBody(uid.Value))
                return new SpellSource(uid.Value, IsArena(uid.Value) ? Transform(uid.Value).MapUid : null);
            if (!TryComp<ProjectileComponent>(uid, out var projectile) || projectile.Shooter == uid)
                break;
            uid = projectile.Shooter;
        }

        return null;
    }

    public SpellSource? ResolveSource(EntityUid? origin)
        => GetSource(origin) ?? (origin is { } body &&
            (HasComp<MobStateComponent>(body) || HasComp<BossActorComponent>(body) ||
             HasComp<BossArenaAttackSourceComponent>(body)) ? null : CurrentSource);
    public bool BlocksFriendlyEffect(EntityUid target, EntityUid? origin = null) => BlocksFriendlyEffect(target, ResolveSource(origin));
    public bool BlocksFriendlyEffect(EntityUid target, SpellSource? source) => source != null && IsArena(target) && IsPartyBody(target);
    public bool BlocksControl(EntityUid target, EntityUid? origin = null) => IsArena(target) && (HasComp<BossArenaProtectedComponent>(target) || BlocksFriendlyEffect(target, origin));
    public void TagSpell(EntityUid spawned, EntityUid? source)
    {
        if (!IsArena(spawned))
            return;
        var ownership = ResolveSource(source);
        if (ownership is not { } owner)
            return;
        var component = EnsureComp<BossArenaSpellSourceComponent>(spawned);
        component.Player = owner.Player;
        component.Arena = Transform(spawned).MapUid;
        Dirty(spawned, component);
    }

    public SourceScope WithSource(EntityUid? source) => WithSource(ResolveSource(source));
    public SourceScope WithSource(SpellSource? source) => new(this, source is { Arena: { } arena } && Exists(arena) && HasComp<BossArenaRulesComponent>(arena) ? source : null);
    public readonly struct SourceScope : IDisposable
    {
        private readonly SharedBossArenaRulesSystem _system;
        private readonly SpellSource? _previous;
        internal SourceScope(SharedBossArenaRulesSystem system, SpellSource? source)
        {
            _system = system;
            _previous = system.CurrentSource;
            system.CurrentSource = source;
        }

        public void Dispose() => _system.CurrentSource = _previous;
    }

    public DamageSpecifier FilterDamage(EntityUid target, DamageSpecifier damage, EntityUid? origin)
    {
        if (!BlocksFriendlyEffect(target, origin) || !damage.AnyPositive())
            return damage;
        var filtered = new DamageSpecifier(damage);
        foreach (var (type, value) in damage.DamageDict)
            if (value > 0)
                filtered.DamageDict.Remove(type);
        return filtered;
    }

    public bool CanSpawn(EntProtoId prototype, EntityCoordinates coordinates) => !IsArena(coordinates) || !IsObstacle(prototype);
    public bool CanSpawn(EntProtoId prototype, MapCoordinates coordinates) => !IsArena(coordinates) || !IsObstacle(prototype);
    public bool IsObstacle(EntProtoId id)
    {
        if (id.Id.StartsWith("MedievalProjectileBlackHole", StringComparison.Ordinal))
            return true;
        if (id.Id is "MedievalShelterSenior" or "MedievalShelterRock" or "MedievalStoneRidgeBeginner" or "MedievalStoneRidgeMiddle" or "MedievalIceBarrierBeginner" or "MedievalIceBarrierMiddle" or "MedievalIceBarrierSenior")
            return true;
        var prototype = _prototypes.Index(id);
        if (prototype.TryGetComponent<BossArenaForbiddenSpellComponent>(out _, Factory))
            return true;
        if (prototype.TryGetComponent<ItemComponent>(out _, Factory) || prototype.TryGetComponent<ProjectileComponent>(out _, Factory) || prototype.TryGetComponent<MobStateComponent>(out _, Factory))
            return false;
        return prototype.TryGetComponent<FixturesComponent>(out var fixtures, Factory) && fixtures.Fixtures.Values.Any(fixture => fixture.Hard && fixture.CollisionLayer != 0);
    }

    public bool CanCast(EntityUid action, EntityUid performer, EntityCoordinates target)
    {
        object? spell = null;
        if (TryComp<WorldTargetActionComponent>(action, out var world))
            spell = world.Event;
        if (spell == null && TryComp<EntityTargetActionComponent>(action, out var entityTarget))
            spell = entityTarget.Event;
        if (spell == null && TryComp<InstantActionComponent>(action, out var instant))
            spell = instant.Event;
        IEnumerable<EntProtoId> prototypes = spell switch
        {
            MedievalProjectileSpellEvent projectile => projectile.ProjectilePrototype.Values,
            MedievalEntityTargetProjectileSpellEvent projectile => new[]
            {
                projectile.ProjectilePrototype
            },
            MedievalHomingProjectilesSpellEvent projectile => new[]
            {
                projectile.ProjectilePrototype
            },
            MedievalSpawnSpellEvent spawn => new[]
            {
                spawn.SpawnedEntityPrototype
            },
            MedievalInstantSpawnEvent spawn => new[]
            {
                spawn.SpawnedEntityPrototype
            },
            MedievalSpawnInHandSpellEvent spawn => new[]
            {
                spawn.SpawnedEntityPrototype
            },
            MedievalSpawnAimingEntityEvent spawn => new[]
            {
                spawn.SpawnedEntity
            },
            _ => Array.Empty<EntProtoId>(),
        };
        return !(IsArena(performer) || IsArena(target)) || !prototypes.Any(IsObstacle);
    }

    public bool CanInsertBody(EntityUid body, BaseContainer container) => !(IsArena(body) && HasComp<BossArenaProtectedComponent>(body)) && (!(IsArena(body) && IsPartyBody(body)) || IsArenaRewardStorage(container.Owner));
    private bool IsArenaRewardStorage(EntityUid storage) => HasComp<BossArenaRewardStorageComponent>(storage) && IsArena(storage);
    public bool CanUseEntityStorage(EntityUid body, EntityUid storage) => !(IsArena(body) && HasComp<BossArenaProtectedComponent>(body)) && (!(IsArena(body) || IsArena(storage)) || IsArenaRewardStorage(storage));
    public bool IsBlockingFurniture(EntityUid uid) => !HasComp<MobStateComponent>(uid) && !HasComp<ProjectileComponent>(uid) && (HasComp<EntityStorageComponent>(uid) || TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == Robust.Shared.Physics.BodyType.Static) && TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.Values.Any(fixture => fixture.Hard && fixture.CollisionLayer != 0);
    public bool CanBringEquipment(EntityUid body)
    {
        var pending = new Stack<EntityUid>();
        var visited = new HashSet<EntityUid>();
        pending.Push(body);
        while (pending.TryPop(out var uid))
        {
            if (!visited.Add(uid))
                continue;
            if (uid != body && IsBlockingFurniture(uid))
                return false;
            if (TryComp<ContainerManagerComponent>(uid, out var manager))
                foreach (var container in manager.Containers.Values)
                    foreach (var contained in container.ContainedEntities)
                        pending.Push(contained);
        }

        return true;
    }

    public void ApplySpellEffect(EntityEffect effect, EntityEffectBaseArgs args, EntityUid source, bool offensive = false)
    {
        using var scope = WithSource(source);
        if (offensive && BlocksFriendlyEffect(args.TargetEntity, source) && effect is not HealthChange)
            return;
        effect.Effect(args);
    }
}
