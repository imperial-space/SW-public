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
    public readonly record struct SpellSource(EntityUid Player, EntityUid? Arena, BossArenaDamageKind DamageKind = BossArenaDamageKind.Direct);
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
        args.HitEntities.RemoveAll(hit => HasComp<BossArenaCenterBlockerComponent>(hit.HitEntity)); // let melee hits reach past the center blocker
    }

    private void OnProtectedInsert(EntityUid uid, BossArenaProtectedComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        if (RuleDenied(uid, BossArenaRule.Control))
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
        if (RuleDenied(uid, BossArenaRule.Control))
            args.Cancel();
    }

    private void OnPetrify(EntityUid uid, BossArenaProtectedComponent component, BeforeTurnetToStone args)
    {
        if (RuleDenied(uid, BossArenaRule.Control))
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
    public bool PreservesArmor(EntityUid target, EntityUid? origin) => origin is { } source && HasComp<BossArenaAttackSourceComponent>(source) && IsArena(source) && IsArena(target) && Transform(source).MapUid == Transform(target).MapUid && RuleDenied(target, BossArenaRule.Armor);
    public bool IsArena(EntityCoordinates coordinates) => IsArena(coordinates.EntityId);
    public bool IsArena(MapCoordinates coordinates)
    {
        var maps = EntityQueryEnumerator<BossArenaRulesComponent, TransformComponent>();
        while (maps.MoveNext(out _, out _, out var xform))
            if (xform.MapID == coordinates.MapId)
                return true;
        return false;
    }

    public bool IsPartyBody(EntityUid uid)                                      // a controlled boss still isn't a party member
        => !HasComp<BossActorComponent>(uid) && !HasComp<BossArenaAttackSourceComponent>(uid) &&
           !HasComp<GhostComponent>(uid) &&
           (HasComp<BossArenaPartyMemberComponent>(uid) || HasComp<ActorComponent>(uid) ||
            TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind);
    public SpellSource? GetSource(EntityUid? uid)
    {
        var kind = BossArenaDamageKind.Direct;
        for (var depth = 0; depth < 4 && uid != null; depth++)
        {
            if (HasComp<BossActorComponent>(uid) || HasComp<BossArenaAttackSourceComponent>(uid))
                return null;
            if (TryComp<BossArenaSpellSourceComponent>(uid, out var source))
                return IsArena(uid.Value) ? new SpellSource(source.Player, Transform(uid.Value).MapUid, source.DamageKind == BossArenaDamageKind.None ? BossArenaDamageKind.Direct : source.DamageKind) : null;
            if (IsPartyBody(uid.Value))
                return new SpellSource(uid.Value, IsArena(uid.Value) ? Transform(uid.Value).MapUid : null, kind);
            if (!TryComp<ProjectileComponent>(uid, out var projectile) || projectile.Shooter == uid)
                break;
            kind = BossArenaDamageKind.Projectiles;
            uid = projectile.Shooter;                                           // follow the shot back to whoever fired it
        }

        return null;
    }

    public SpellSource? ResolveSource(EntityUid? origin)
    {
        var source = GetSource(origin);
        if (source is { } known)
            return CurrentSource is { } current && current.Player == known.Player ? current : known;
        return origin is { } body && (HasComp<MobStateComponent>(body) || HasComp<BossActorComponent>(body) ||
            HasComp<BossArenaAttackSourceComponent>(body)) ? null : CurrentSource;
    }

    public SpellSource? ResolveSource(EntityUid? origin, BossArenaDamageKind kind)
        => ResolveSource(origin) is { } source ? source with { DamageKind = kind } : null;

    public bool RuleDenied(MapCoordinates target, BossArenaRule rule)
    {
        var maps = EntityQueryEnumerator<BossArenaRulesComponent, TransformComponent>();
        while (maps.MoveNext(out var uid, out _, out var xform))
            if (xform.MapID == target.MapId) return RuleDenied(uid, rule);
        return false;
    }

    public bool RuleDenied(EntityCoordinates target, BossArenaRule rule)
        => RuleDenied(target.EntityId, rule);

    public bool RuleDenied(EntityUid target, BossArenaRule rule, BossArenaDamageKind kind = BossArenaDamageKind.Direct)
    {
        var map = HasComp<BossArenaRulesComponent>(target) ? target :
            TryComp<TransformComponent>(target, out var xform) ? xform.MapUid : null;
        if (map == null || !HasComp<BossArenaRulesComponent>(map)) return false;
        var request = new BossArenaRuleEvent(Comp<BossArenaRulesComponent>(map.Value), rule, kind);
        RaiseLocalEvent(ref request);                                  // no installed rule means normal behavior
        return request.Denied;
    }

    public bool BlocksFriendlyEffect(EntityUid target, EntityUid? origin = null)
        => BlocksFriendlyEffect(target, ResolveSource(origin));
    public bool BlocksFriendlyEffect(EntityUid target, EntityUid? origin, BossArenaDamageKind kind)
        => BlocksFriendlyEffect(target, ResolveSource(origin) is { } source ? source with { DamageKind = kind } : null);
    public bool BlocksFriendlyEffect(EntityUid target, SpellSource? source)
        => source is { } owner && IsPartyBody(target) && RuleDenied(target, BossArenaRule.FriendlyDamage, owner.DamageKind);
    public bool BlocksControl(EntityUid target, EntityUid? origin = null)
        => (HasComp<BossArenaProtectedComponent>(target) || ResolveSource(origin) != null && IsPartyBody(target)) &&
            RuleDenied(target, BossArenaRule.Control);
    public void TagSpell(EntityUid spawned, EntityUid? source, BossArenaDamageKind kind = BossArenaDamageKind.Spells)
    {
        if (!IsArena(spawned))
            return;
        var ownership = ResolveSource(source);
        if (ownership is not { } owner)
            return;
        var component = EnsureComp<BossArenaSpellSourceComponent>(spawned);
        component.Player = owner.Player;                                        // remember the player for later hits and explosions
        component.Arena = Transform(spawned).MapUid;
        component.DamageKind = kind;
        Dirty(spawned, component);
    }

    public SourceScope WithSource(EntityUid? source) => WithSource(ResolveSource(source));
    public SourceScope WithSource(EntityUid? source, BossArenaDamageKind kind)
        => WithSource(ResolveSource(source) is { } owner ? owner with { DamageKind = kind } : null);
    public SourceScope WithSource(SpellSource? source, BossArenaDamageKind kind)
        => WithSource(source is { } owner ? owner with { DamageKind = kind } : null);
    public SourceScope WithSource(SpellSource? source) => new(this, source is { Arena: { } arena } && Exists(arena) && HasComp<BossArenaRulesComponent>(arena) ? source : null);
    public readonly struct SourceScope : IDisposable
    {
        private readonly SharedBossArenaRulesSystem _system;
        private readonly SpellSource? _previous;
        internal SourceScope(SharedBossArenaRulesSystem system, SpellSource? source)
        {
            _system = system;
            _previous = system.CurrentSource;
            system.CurrentSource = source;                                       // use this owner for nested damage effects
        }

        public void Dispose() => _system.CurrentSource = _previous;             // put the old source back after nested effects
    }

    public DamageSpecifier FilterDamage(EntityUid target, DamageSpecifier damage, EntityUid? origin)
    {
        if (!BlocksFriendlyEffect(target, origin) || !damage.AnyPositive())
            return damage;
        var filtered = new DamageSpecifier(damage);
        foreach (var (type, value) in damage.DamageDict)
            if (value > 0)
                filtered.DamageDict.Remove(type);                               // block friendly damage, leave healing alone
        return filtered;
    }

    public bool CanSpawn(EntProtoId prototype, EntityCoordinates coordinates) => !RuleDenied(coordinates.EntityId, BossArenaRule.Building) || !IsObstacle(prototype);
    public bool CanSpawn(EntProtoId prototype, MapCoordinates coordinates) => !RuleDenied(coordinates, BossArenaRule.Building) || !IsObstacle(prototype);
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
        return !(RuleDenied(performer, BossArenaRule.Building) || RuleDenied(target.EntityId, BossArenaRule.Building)) || !prototypes.Any(IsObstacle);
    }

    public bool CanInsertBody(EntityUid body, BaseContainer container) => !(RuleDenied(body, BossArenaRule.Control) && HasComp<BossArenaProtectedComponent>(body)) && (!(RuleDenied(body, BossArenaRule.Equipment) && IsPartyBody(body)) || IsArenaRewardStorage(container.Owner));
    private bool IsArenaRewardStorage(EntityUid storage) => HasComp<BossArenaRewardStorageComponent>(storage) && IsArena(storage);
    public bool CanUseEntityStorage(EntityUid body, EntityUid storage) => !(RuleDenied(body, BossArenaRule.Control) && HasComp<BossArenaProtectedComponent>(body)) && (!(RuleDenied(body, BossArenaRule.Equipment) || RuleDenied(storage, BossArenaRule.Equipment)) || IsArenaRewardStorage(storage));
    public bool IsBlockingFurniture(EntityUid uid) => !HasComp<MobStateComponent>(uid) && !HasComp<ProjectileComponent>(uid) && (HasComp<EntityStorageComponent>(uid) || TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == Robust.Shared.Physics.BodyType.Static) && TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.Values.Any(fixture => fixture.Hard && fixture.CollisionLayer != 0);
    public bool CanBringEquipment(EntityUid body, EntityUid? arena = null)
    {
        if (!RuleDenied(arena ?? body, BossArenaRule.Equipment)) return true;
        var pending = new Stack<EntityUid>();
        var visited = new HashSet<EntityUid>();
        pending.Push(body);
        while (pending.TryPop(out var uid))
        {
            if (!visited.Add(uid))                                               // avoid checking the same contained item twice
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
        using var scope = WithSource(source, BossArenaDamageKind.Spells);
        if (offensive && BlocksFriendlyEffect(args.TargetEntity, source) && effect is not HealthChange)
            return;
        effect.Effect(args);
    }
}
