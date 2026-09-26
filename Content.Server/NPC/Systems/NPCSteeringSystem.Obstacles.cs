using System.Numerics;
using Content.Server.Destructible;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Climbing;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.NPC;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ClimbableComponent = Content.Shared.Climbing.Components.ClimbableComponent;
using ClimbingComponent = Content.Shared.Climbing.Components.ClimbingComponent;

// Imperial Medieval npc-obstacle-handling: reworked end to end, treat this file as fork-owned.

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    /*
     * For any custom path handlers, e.g. destroying walls, opening airlocks, etc.
     * Putting it onto steering seemed easier than trying to make a custom compound task for it.
     * I also considered task interrupts although the problem is handling stuff like pathfinding overlaps
     * Ideally we could do interrupts but that's TODO.
     */

    /*
     * TODO:
     * - Add path cap
     * - Circle cast BFS in LOS to determine targets.
     * - Store last known coordinates of X targets.
     * - Require line of sight for melee
     * - Add new behavior where they move to melee target's last known position (diffing theirs and current)
     *  then do the thing like from dishonored where it gets passed to a search system that opens random stuff.
     */

    private static readonly NPCObstacleSmashingComponent DefaultSmashing = new();

    // Must match what PathfindingSystem.BuildBreadcrumbs counts.
    private const LookupFlags ObstacleLookupFlags = LookupFlags.Static | LookupFlags.Dynamic;

    private bool IsPursuing(EntityUid uid)
    {
        if (_meleeCombatQuery.HasComponent(uid) || _rangedCombatQuery.HasComponent(uid))
            return true;

        // Combat components only exist while the combat operator runs.
        return _targetMemoryQuery.TryGetComponent(uid, out var memory) && memory.Target != null;
    }

    private bool CanSmashAnything(EntityUid uid, NPCObstacleSmashingComponent settings)
    {
        return !settings.RequireTarget || IsPursuing(uid);
    }

    private NPCObstacleSmashingComponent GetSmashSettings(EntityUid uid)
    {
        return CompOrNull<NPCObstacleSmashingComponent>(uid) ?? DefaultSmashing;
    }

    private bool CanSmash(EntityUid uid, EntityUid target, NPCObstacleSmashingComponent settings, DamageSpecifier damage, float attackRate)
    {
        if (!_destructibleQuery.TryGetComponent(target, out var destructible))
            return false;

        if (_unsmashableQuery.HasComponent(target))
            return false;

        if (!_whitelist.IsWhitelistPassOrNull(settings.Whitelist, target) ||
            _whitelist.IsBlacklistPass(settings.Blacklist, target))
        {
            return false;
        }

        if (!TryComp<DamageableComponent>(target, out var damageable))
            return false;

        var effective = damage;

        if (damageable.DamageModifierSetId is { } modifierId &&
            _protoManager.TryIndex<DamageModifierSetPrototype>(modifierId, out var modifiers))
        {
            effective = DamageSpecifier.ApplyModifierSet(damage, modifiers);
        }

        var perHit = FixedPoint2.Zero;

        foreach (var (type, amount) in effective.DamageDict)
        {
            if (amount <= FixedPoint2.Zero || !damageable.Damage.DamageDict.ContainsKey(type))
                continue;

            perHit += amount;
        }

        if (perHit < settings.MinEffectiveDamage)
            return false;

        var threshold = _destructible.DestroyedAt(target, destructible);

        if (threshold == FixedPoint2.MaxValue)
            return false;

        var remaining = threshold - damageable.TotalDamage;

        if (remaining <= FixedPoint2.Zero)
            return true;

        return (remaining / (perHit + FixedPoint2.Epsilon)).Float() / MathF.Max(attackRate, 0.01f) <= settings.MaxSmashSeconds;
    }

    private SteeringObstacleStatus TryHandleFlags(EntityUid uid, NPCSteeringComponent component, PathPoly poly, PhysicsComponent body)
    {
        DebugTools.Assert(!poly.Data.IsFreeSpace);
        // TODO: Store PathFlags on the steering comp
        // and be able to re-check it.

        var layer = 0;
        var mask = 0;

        if (TryComp<FixturesComponent>(uid, out var manager))
        {
            (layer, mask) = _physics.GetHardCollision(uid, manager);
        }
        else
        {
            return SteeringObstacleStatus.Failed;
        }

        // TODO: Should cache the fact we're doing this somewhere.
        // See https://github.com/space-wizards/space-station-14/issues/11475
        if ((poly.Data.CollisionLayer & mask) == 0x0 &&
            (poly.Data.CollisionMask & layer) == 0x0)
        {
            return SteeringObstacleStatus.Completed;
        }

        var id = component.DoAfterId;

        // Still doing what we were doing before.
        var doAfterStatus = _doAfter.GetStatus(id);

        switch (doAfterStatus)
        {
            case DoAfterStatus.Running:
                component.LastObstacleProgress = _timing.CurTime;
                return SteeringObstacleStatus.Stationary;
            case DoAfterStatus.Cancelled:
                return SteeringObstacleStatus.Failed;
        }

        component.DoAfterId = null;

        var obstacleEnts = new List<EntityUid>();

        GetObstacleEntities(uid, poly, mask, layer, obstacleEnts);

        if (obstacleEnts.Count == 0)
            return SteeringObstacleStatus.Completed;

        var isDoor = (poly.Data.Flags & PathfindingBreadcrumbFlag.Door) != 0x0;
        var isAccessRequired = (poly.Data.Flags & PathfindingBreadcrumbFlag.Access) != 0x0;
        var isClimbable = (poly.Data.Flags & PathfindingBreadcrumbFlag.Climb) != 0x0;

        // Just walk into it stupid
        if (isDoor && !isAccessRequired && (component.Flags & PathFlags.Interact) != 0x0)
        {
            var doorQuery = GetEntityQuery<DoorComponent>();

            // ... At least if it's not a bump open.
            foreach (var ent in obstacleEnts)
            {
                if (!doorQuery.TryGetComponent(ent, out var door) || door.BumpOpen)
                    continue;

                if (door.State == DoorState.Opening)
                {
                    component.LastObstacleProgress = _timing.CurTime;
                    return SteeringObstacleStatus.Stationary;
                }

                _interaction.InteractionActivate(uid, ent);
                return SteeringObstacleStatus.Continuing;
            }

            // If we get to here then didn't succeed for reasons.
        }

        if ((component.Flags & PathFlags.Prying) != 0x0 && isDoor && IsPursuing(uid))
        {
            if (body.LinearVelocity.LengthSquared() > 0.01f)
                return SteeringObstacleStatus.Stationary;

            var doorQuery = GetEntityQuery<DoorComponent>();

            // Get the relevant obstacle
            foreach (var ent in obstacleEnts)
            {
                if (!doorQuery.TryGetComponent(ent, out var door))
                    continue;

                if (door.State != DoorState.Open)
                {
                    // TODO: Use the verb.

                    if (door.State == DoorState.Opening)
                    {
                        component.DoAfterId = null;
                        component.LastObstacleProgress = _timing.CurTime;
                        return SteeringObstacleStatus.Stationary;
                    }

                    _pryingSystem.TryPry(ent, uid, out id, uid);

                    // TryPry reports true with a null id when it refuses.
                    if (id == null)
                        continue;

                    component.DoAfterId = id;
                    component.LastObstacleProgress = _timing.CurTime;
                    // The pry do_after is BreakOnMove, so Continuing would cancel it on the spot.
                    return SteeringObstacleStatus.Stationary;
                }
            }
        }

        // Try climbing obstacles
        if ((component.Flags & PathFlags.Climbing) != 0x0 && isClimbable)
        {
            if (body.LinearVelocity.LengthSquared() > 0.01f)
                return SteeringObstacleStatus.Stationary;

            if (TryComp<ClimbingComponent>(uid, out var climbing))
            {
                if (climbing.IsClimbing)
                {
                    return SteeringObstacleStatus.Completed;
                }

                if (climbing.NextTransition != null)
                {
                    component.LastObstacleProgress = _timing.CurTime;
                    return SteeringObstacleStatus.Stationary;
                }

                var climbableQuery = GetEntityQuery<ClimbableComponent>();

                // Get the relevant obstacle
                foreach (var ent in obstacleEnts)
                {
                    if (climbableQuery.TryGetComponent(ent, out var table) &&
                        _climb.CanVault(table, uid, uid, out _) &&
                        _climb.TryClimb(uid, uid, ent, out id, table, climbing))
                    {
                        component.DoAfterId = id;
                        component.LastObstacleProgress = _timing.CurTime;
                        return SteeringObstacleStatus.Stationary;
                    }
                }
            }

        }

        // Try smashing obstacles.
        if ((component.Flags & PathFlags.Smashing) != 0x0 &&
            _melee.TryGetWeapon(uid, out _, out var meleeWeapon) &&
            HasComp<CombatModeComponent>(uid))
        {
            // The navmesh lags a beat behind us breaking something.
            if (_interaction.InRangeUnobstructed(uid, GetCoordinates(poly), SharedInteractionSystem.InteractionRange, (CollisionGroup) mask))
                return SteeringObstacleStatus.Completed;

            if (!CanSmashAnything(uid, GetSmashSettings(uid)))
                return SteeringObstacleStatus.Failed;

            if (meleeWeapon.NextAttack > _timing.CurTime)
                return SteeringObstacleStatus.Stationary;

            if (TrySwingAtObstacle(uid, obstacleEnts, Vector2.Zero, out var hadValidTarget, out _))
            {
                component.LastObstacleProgress = _timing.CurTime;
                return SteeringObstacleStatus.Stationary;
            }

            if (hadValidTarget)
                return SteeringObstacleStatus.Approaching;
        }

        return SteeringObstacleStatus.Failed;
    }

    private void GetObstacleEntities(EntityUid uid, PathPoly poly, int mask, int layer, List<EntityUid> ents)
    {
        if (!HasComp<MapGridComponent>(poly.GraphUid))
        {
            return;
        }

        // Trees, railings and closets are static but unanchored.
        var found = _entSetPool.Get();
        _lookup.GetLocalEntitiesIntersecting(poly.GraphUid, poly.Box, found, ObstacleLookupFlags);
        FilterObstacles(uid, found, mask, layer, ents);
        _entSetPool.Return(found);
        SortNearestFirst(uid, ents);
    }

    private void GetBlockingEntities(EntityUid uid, float range, int mask, int layer, List<EntityUid> ents)
    {
        var found = _entSetPool.Get();

        _physics.GetContactingEntities((uid, null), found);

        if (found.Count == 0)
            _lookup.GetEntitiesInRange(uid, range, found, ObstacleLookupFlags);

        FilterObstacles(uid, found, mask, layer, ents);
        _entSetPool.Return(found);
        SortNearestFirst(uid, ents);
    }

    private void FilterObstacles(EntityUid uid, HashSet<EntityUid> found, int mask, int layer, List<EntityUid> ents)
    {
        foreach (var ent in found)
        {
            if (ent == uid)
                continue;

            // Large mobs share collision layers with walls, so the mask check below won't catch them.
            if (HasComp<MobStateComponent>(ent))
                continue;

            if (!_physicsQuery.TryGetComponent(ent, out var body) ||
                !body.Hard ||
                !body.CanCollide ||
                (body.CollisionMask & layer) == 0x0 && (body.CollisionLayer & mask) == 0x0)
            {
                continue;
            }

            ents.Add(ent);
        }
    }

    private void SortNearestFirst(EntityUid uid, List<EntityUid> ents)
    {
        if (ents.Count <= 1)
            return;

        var origin = _transform.GetWorldPosition(uid);
        ents.Sort((a, b) =>
            Vector2.DistanceSquared(origin, _transform.GetWorldPosition(a))
                .CompareTo(Vector2.DistanceSquared(origin, _transform.GetWorldPosition(b))));
    }

    /// <summary>
    /// A non-zero <paramref name="heading"/> ignores candidates behind us.
    /// </summary>
    private bool TrySwingAtObstacle(EntityUid uid, List<EntityUid> candidates, Vector2 heading,
        out bool hadValidTarget, out bool destroyedTarget)
    {
        hadValidTarget = false;
        destroyedTarget = false;

        if (!_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) ||
            !TryComp<CombatModeComponent>(uid, out var combatMode))
        {
            return false;
        }

        var settings = GetSmashSettings(uid);
        var damage = _melee.GetDamage(weaponUid, uid, weapon);
        var attackRate = _melee.GetAttackRate(weaponUid, uid, weapon);
        var origin = _transform.GetWorldPosition(uid);

        foreach (var ent in candidates)
        {
            if (!CanSmash(uid, ent, settings, damage, attackRate))
                continue;

            if (heading != Vector2.Zero)
            {
                var toEnt = _transform.GetWorldPosition(ent) - origin;

                if (toEnt.LengthSquared() > 0f && Vector2.Dot(toEnt.Normalized(), heading) < 0f)
                    continue;
            }

            hadValidTarget = true;

            if (!_interaction.InRangeUnobstructed(uid, ent, weapon.Range))
                continue;

            // NPCCombatSystem strips the component off anything out of combat mode.
            var wasInCombatMode = combatMode.IsInCombatMode;
            _combat.SetInCombatMode(uid, true, combatMode);

            var hit = _melee.AttemptLightAttack(uid, weaponUid, weapon, ent);

            if (!wasInCombatMode)
                _combat.SetInCombatMode(uid, false, combatMode);

            // DestroyEntity only queues deletion, so Deleted() alone misses a kill from this tick.
            destroyedTarget = hit && (EntityManager.IsQueuedForDeletion(ent) || Deleted(ent));

            return hit;
        }

        return false;
    }

    /// <summary>
    /// For when we're wedged against something the path reads as free space.
    /// </summary>
    private bool TrySmashBlockingContact(EntityUid uid, NPCSteeringComponent component, Vector2 direction, out bool destroyedTarget)
    {
        destroyedTarget = false;

        if ((component.Flags & PathFlags.Smashing) == 0x0 ||
            !_melee.TryGetWeapon(uid, out _, out var meleeWeapon) ||
            !TryComp<FixturesComponent>(uid, out var manager))
        {
            return false;
        }

        if (!CanSmashAnything(uid, GetSmashSettings(uid)))
            return false;

        var (layer, mask) = _physics.GetHardCollision(uid, manager);
        var ents = new List<EntityUid>();

        GetBlockingEntities(uid, meleeWeapon.Range, mask, layer, ents);

        if (ents.Count == 0)
            return false;

        if (meleeWeapon.NextAttack > _timing.CurTime)
            return true;

        var heading = direction.LengthSquared() > 0f ? direction.Normalized() : Vector2.Zero;

        if (!TrySwingAtObstacle(uid, ents, heading, out _, out destroyedTarget))
            return false;

        component.LastObstacleProgress = _timing.CurTime;
        return true;
    }

    /// <summary>
    /// Returns true while it's swinging. The obstacle handling can't do this, it skips mobs on purpose.
    /// </summary>
    private bool TryFightHolder(EntityUid uid, EntityUid holder)
    {
        // Same rule as NPCRetaliationSystem.
        if (_npcFaction.IsEntityFriendly(uid, holder))
            return false;

        if (!_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) ||
            !TryComp<CombatModeComponent>(uid, out var combatMode))
        {
            return false;
        }

        if (weapon.NextAttack > _timing.CurTime)
            return true;

        if (!_interaction.InRangeUnobstructed(uid, holder, weapon.Range))
            return false;

        var wasInCombatMode = combatMode.IsInCombatMode;
        _combat.SetInCombatMode(uid, true, combatMode);

        var hit = _melee.AttemptLightAttack(uid, weaponUid, weapon, holder);

        if (!wasInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);

        return hit;
    }

    private enum SteeringObstacleStatus : byte
    {
        Completed,
        Failed,

        /// <summary>Working on it, movement still allowed.</summary>
        Continuing,

        /// <summary>Working on it, but we have to stop moving.</summary>
        Stationary,

        /// <summary>Valid obstacle, out of reach. Close the distance instead of dropping the route.</summary>
        Approaching
    }
}
