using System.Numerics;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Dragons.TailSweep;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Content.Shared.TDMNaming.Components;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Dragons.TailSweep;

/// <summary>
/// Server part of the tail sweep: spins the dragon in place, performs the hit and lets
/// AI-controlled dragons use it on their own.
/// </summary>
public sealed class MedievalTailSweepSystem : SharedMedievalTailSweepSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private readonly HashSet<Entity<MobStateComponent>> _targets = new();
    private readonly HashSet<Entity<DamageableComponent>> _structures = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<MedievalTailSweepComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.SweepEnd is { } end)
            {
                UpdateSpin((uid, comp), now);

                if (now >= end)
                    FinishSweep((uid, comp));

                continue;
            }

            if (now < comp.NextAutoCheck)
                continue;

            comp.NextAutoCheck = now + comp.AutoCheckInterval;
            TryAutoSweep((uid, comp));
        }
    }

    protected override void OnSweepStarted(Entity<MedievalTailSweepComponent> ent)
    {
        if (ent.Comp.WindupEffect is not { } effect)
            return;

        // The trail is not a child of the dragon: the dragon turns, the trail has to stay as it was at the start.
        var trail = SpawnAtPosition(effect, Transform(ent.Owner).Coordinates);
        Xform.SetWorldRotation(trail, ent.Comp.StartRotation);
    }

    /// <summary>
    /// Rotates the dragon step by step so its 4-directional sprite looks like it spins in place.
    /// </summary>
    private void UpdateSpin(Entity<MedievalTailSweepComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.SpinSteps <= 0)
            return;

        var stepTime = comp.Windup / comp.SpinSteps;
        while (comp.SpinStepsDone < comp.SpinSteps && now >= comp.NextSpinStep)
        {
            comp.SpinStepsDone++;
            comp.NextSpinStep += stepTime;

            var turn = Math.Tau * comp.SpinStepsDone / comp.SpinSteps;
            Xform.SetWorldRotation(ent.Owner, comp.StartRotation + new Angle(turn));
        }
    }

    private void FinishSweep(Entity<MedievalTailSweepComponent> ent)
    {
        var comp = ent.Comp;
        comp.SweepEnd = null;
        Dirty(ent);

        if (TerminatingOrDeleted(ent.Owner))
            return;

        Xform.SetWorldRotation(ent.Owner, comp.StartRotation);
        Blocker.UpdateCanMove(ent.Owner);

        // No hit if the dragon was killed, stunned or put in a container during the wind-up.
        if (!CanSweepNow(ent.Owner))
            return;

        DoHit(ent);
    }

    private bool CanSweepNow(EntityUid uid)
    {
        return _mobState.IsAlive(uid)
               && Blocker.CanConsciouslyPerformAction(uid)
               && Blocker.CanInteract(uid, null)
               && !_container.IsEntityOrParentInContainer(uid)
               && Transform(uid).MapID != MapId.Nullspace;
    }

    private void DoHit(Entity<MedievalTailSweepComponent> ent)
    {
        var comp = ent.Comp;
        var coords = Transform(ent.Owner).Coordinates;
        var center = Xform.GetWorldPosition(ent.Owner);

        Audio.PlayPvs(comp.HitSound, ent.Owner);
        if (comp.Effect is { } effect)
            SpawnAtPosition(effect, coords);

        _targets.Clear();
        _lookup.GetEntitiesInRange(coords, comp.Range, _targets, LookupFlags.Uncontained);

        foreach (var target in _targets)
        {
            if (!IsValidTarget(ent, target))
                continue;

            if (!comp.Damage.Empty)
                _damageable.TryChangeDamage(target.Owner, comp.Damage, origin: ent.Owner);

            // The damage may have killed and deleted the target.
            if (TerminatingOrDeleted(target.Owner))
                continue;

            _stun.TryUpdateParalyzeDuration(target.Owner, comp.StunTime);

            if (HasComp<MedievalUnthrowableComponent>(target.Owner))
                continue;

            var direction = Xform.GetWorldPosition(target.Owner) - center;
            if (direction.LengthSquared() < 0.0001f)
                direction = comp.StartRotation.ToWorldVec();

            _throwing.TryThrow(target.Owner, Vector2.Normalize(direction) * comp.ThrowDistance, comp.ThrowSpeed, ent.Owner, pushbackRatio: 0f, recoil: false);
        }

        _targets.Clear();

        if (!comp.StructureDamage.Empty)
            HitStructures(ent, coords);
    }

    /// <summary>
    /// Deals structural damage to anchored, solid structures in range that the dragon can see.
    /// </summary>
    private void HitStructures(Entity<MedievalTailSweepComponent> ent, EntityCoordinates coords)
    {
        var comp = ent.Comp;
        _structures.Clear();
        _lookup.GetEntitiesInRange(coords, comp.Range, _structures, LookupFlags.Static);

        foreach (var structure in _structures)
        {
            var uid = structure.Owner;
            if (uid == ent.Owner || TerminatingOrDeleted(uid))
                continue;

            if (HasComp<MobStateComponent>(uid) || !Transform(uid).Anchored)
                continue;

            // Only things that physically block the way: walls, doors, tables. Leave subfloor cables and pipes alone.
            if (!TryComp<PhysicsComponent>(uid, out var physics) || !physics.CanCollide || !physics.Hard)
                continue;

            if (_whitelist.IsBlacklistPass(comp.StructureBlacklist, uid))
                continue;

            // Don't break walls hidden behind other walls.
            if (!_interaction.InRangeUnobstructed(ent.Owner, uid, comp.Range + 1f))
                continue;

            _damageable.TryChangeDamage(uid, comp.StructureDamage, origin: ent.Owner);
        }

        _structures.Clear();
    }

    private bool IsValidTarget(Entity<MedievalTailSweepComponent> ent, Entity<MobStateComponent> target)
    {
        var uid = target.Owner;
        if (uid == ent.Owner || TerminatingOrDeleted(uid))
            return false;

        if (_mobState.IsDead(uid, target.Comp))
            return false;

        // Skip anything riding or attached to the dragon.
        if (Transform(uid).ParentUid == ent.Owner)
            return false;

        // Ignore ghosts and incorporeal mobs.
        if (!TryComp<PhysicsComponent>(uid, out var physics)
            || (physics.CollisionLayer & (int) CollisionGroup.GhostImpassable) != 0)
            return false;

        if (!ent.Comp.HitFriendly && _faction.IsEntityFriendly(ent.Owner, uid))
            return false;

        // Don't hit through walls.
        return _interaction.InRangeUnobstructed(ent.Owner, uid, ent.Comp.Range + 1f);
    }

    /// <summary>
    /// An AI dragon sweeps on its own when enemies get close. Player dragons press the action themselves.
    /// </summary>
    private void TryAutoSweep(Entity<MedievalTailSweepComponent> ent)
    {
        if (HasComp<ActorComponent>(ent.Owner))
            return;

        if (ent.Comp.ActionEntity is not { } actionUid
            || !TryComp<ActionComponent>(actionUid, out var action)
            || !action.Enabled
            || Actions.IsCooldownActive(action, Timing.CurTime))
            return;

        if (!CanSweepNow(ent.Owner))
            return;

        var coords = Transform(ent.Owner).Coordinates;
        _targets.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.AutoRange, _targets, LookupFlags.Uncontained);

        var count = 0;
        foreach (var target in _targets)
        {
            if (IsValidTarget(ent, target))
                count++;
        }
        _targets.Clear();

        if (count < Math.Max(1, ent.Comp.AutoMinTargets))
            return;

        // Go through the action so the cooldown is shared with player use.
        Actions.PerformAction(ent.Owner, (actionUid, action), predicted: false);
    }
}
