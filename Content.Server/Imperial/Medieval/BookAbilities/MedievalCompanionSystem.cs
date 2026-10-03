using System.Linq;
using System.Numerics;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Content.Server.Imperial.Medieval.Boss;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;
using Content.Shared.RatKing;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.BookAbilities;

[RegisterComponent]
public sealed partial class MedievalUntameableComponent : Component;

[RegisterComponent]
public sealed partial class MedievalCompanionComponent : Component
{
    [DataField] public EntityUid Master;
    [DataField] public string Order = "follow";
    [DataField] public EntityUid? GuardTarget;
    public EntityUid? CommandedEnemy;
}

[RegisterComponent]
public sealed partial class MedievalGuardedTargetComponent : Component;

[RegisterComponent]
public sealed partial class MedievalCompanionOwnerComponent : Component
{
    [DataField] public List<EntityUid> Actions = new();
}

[RegisterComponent]
public sealed partial class MedievalPacifiedBeastComponent : Component
{
    [DataField] public TimeSpan Until;
    [DataField] public bool WasEnabled;
}

/// <summary>Commands operate on the actual creature: no replacement mob and no copied combat stats.</summary>
public sealed class MedievalCompanionSystem : EntitySystem
{
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    private TimeSpan _nextGuardReview;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalCompanionOwnerComponent, BookCompanionOrderEvent>(OnOrder);
        SubscribeLocalEvent<MedievalCompanionOwnerComponent, BookCompanionAttackEvent>(OnAttack);
        SubscribeLocalEvent<MedievalCompanionOwnerComponent, BookCompanionGuardEvent>(OnGuard);
        SubscribeLocalEvent<MedievalGuardedTargetComponent, DamageChangedEvent>(OnGuardedTargetHurt);
        SubscribeLocalEvent<MedievalCompanionComponent, ComponentShutdown>(OnCompanionShutdown);
        SubscribeLocalEvent<MedievalPacifiedBeastComponent, DamageChangedEvent>(OnHurt);
    }

    public int OwnedCount(EntityUid owner) => EntityQuery<MedievalCompanionComponent>()
        .Count(c => c.Master == owner && !TerminatingOrDeleted(c.Owner) &&
                    TryComp<MobStateComponent>(c.Owner, out var state) && state.CurrentState != MobState.Dead);

    public bool CanTame(EntityUid beast, EntityUid owner)
    {
        if (beast == owner || !HasComp<HTNComponent>(beast) || HasComp<HumanoidAppearanceComponent>(beast) ||
            HasComp<BossComponent>(beast) || HasComp<MedievalUntameableComponent>(beast))
            return false;
        if (TryComp<MindContainerComponent>(beast, out var mind) && mind.HasMind)
            return false;
        if (!TryComp<MobStateComponent>(beast, out var state) || state.CurrentState != MobState.Alive)
            return false;
        return !TryComp<MedievalCompanionComponent>(beast, out var pet) || pet.Master == owner;
    }

    public bool Tame(EntityUid beast, EntityUid owner, int limit = 1)
    {
        if (!CanTame(beast, owner) || (!HasComp<MedievalCompanionComponent>(beast) && OwnedCount(owner) >= limit))
            return false;
        if (!Bind(beast, owner)) return false;
        RemComp<Content.Shared.Imperial.Medieval.Additions.MedievalTimedDespawnComponent>(beast);
        return true;
    }

    /// <summary>Used by magically animated objects. The caller supplies a real HTN/movement body.</summary>
    public bool Bind(EntityUid beast, EntityUid owner)
    {
        if (!TryComp<HTNComponent>(beast, out var htn) || Deleted(owner))
            return false;
        EndPacification(beast);
        ResetPlan(htn);
        if (TryComp<FactionExceptionComponent>(beast, out var exceptions))
            foreach (var enemy in exceptions.Hostiles.ToArray()) _factions.DeAggroEntity(beast, enemy);
        var pet = EnsureComp<MedievalCompanionComponent>(beast);
        pet.Master = owner;
        htn.RootTask = new HTNCompoundTask { Task = "RatServantCompound" };
        htn.Enabled = true;
        if (TryComp<NpcFactionMemberComponent>(beast, out var member))
        {
            foreach (var faction in member.Factions.ToArray())
                _factions.RemoveFaction(beast, faction);
        }
        if (TryComp<NpcFactionMemberComponent>(owner, out var ownersFaction))
            _factions.AddFactions(beast, new(ownersFaction.Factions));
        _factions.IgnoreEntity(beast, owner);
        _npc.SetBlackboard(beast, NPCBlackboard.FollowTarget, new EntityCoordinates(owner, Vector2.Zero));
        EnsureOwner(owner);
        IssueOrder(beast, "follow");
        _npc.WakeNPC(beast, htn);
        return true;
    }

    private void EnsureOwner(EntityUid owner)
    {
        if (HasComp<MedievalCompanionOwnerComponent>(owner))
            return;
        var comp = AddComp<MedievalCompanionOwnerComponent>(owner);
        foreach (var id in new[] { "ActionBookCompanionFollow", "ActionBookCompanionStay", "ActionBookCompanionGuard", "ActionBookCompanionRetreat", "ActionBookCompanionAttack" })
        {
            EntityUid? action = null;
            _actions.AddAction(owner, ref action, id);
            if (action != null) comp.Actions.Add(action.Value);
        }
    }

    public void IssueOrder(EntityUid beast, string order, EntityUid? target = null)
    {
        if (!TryComp<MedievalCompanionComponent>(beast, out var pet) || !TryComp<HTNComponent>(beast, out var htn))
            return;
        if (order == "guard" && (target is not { } guarded || guarded == beast ||
                                TerminatingOrDeleted(guarded) || !HasComp<DamageableComponent>(guarded)))
            return;
        ResetPlan(htn);
        ClearGuardTarget(pet);
        pet.Order = order;
        if (order == "guard")
        {
            pet.GuardTarget = target;
            EnsureComp<MedievalGuardedTargetComponent>(target!.Value);
        }
        var type = order switch
        {
            "stay" => RatKingOrderType.Stay,
            "attack" => RatKingOrderType.CheeseEm,
            _ => RatKingOrderType.Follow
        };
        _npc.SetBlackboard(beast, NPCBlackboard.CurrentOrders, type);
        _npc.SetBlackboard(beast, NPCBlackboard.FollowTarget, new EntityCoordinates(pet.GuardTarget ?? pet.Master, Vector2.Zero));
        if (order == "attack" && target != null && target != pet.Master)
        {
            _npc.SetBlackboard(beast, NPCBlackboard.CurrentOrderedTarget, target.Value);
            // OrderedTargets first filters by hostility, even for an explicit command against a fellow faction member.
            _factions.AggroEntity(beast, target.Value);
            pet.CommandedEnemy = target.Value;
        }
        else
            htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
        // Native RatKing Stay runs IdleCompound, which wanders. A waiting companion must actually stop.
        _npc.SetBlackboard(beast, "IdleRange", order == "guard" ? 0f : 1f);
        _npc.SetBlackboard(beast, "FollowCloseRange", 1f);
        _npc.SetBlackboard(beast, "FollowRange", 2f);
        ResumeBrain(beast, htn, pet);
    }

    private void ResumeBrain(EntityUid beast, HTNComponent htn, MedievalCompanionComponent pet)
    {
        var enabled = pet.Order != "stay" && !HasComp<MedievalPacifiedBeastComponent>(beast);
        _htn.SetHTNEnabled((beast, htn), enabled);
        if (enabled) _htn.Replan(htn);
    }

    private void OnOrder(EntityUid uid, MedievalCompanionOwnerComponent comp, BookCompanionOrderEvent args)
    {
        if (args.Handled) return;
        foreach (var pet in EntityQuery<MedievalCompanionComponent>().Where(c => c.Master == uid).ToArray())
            IssueOrder(pet.Owner, args.Order);
        args.Handled = true;
    }

    private void OnAttack(EntityUid uid, MedievalCompanionOwnerComponent comp, BookCompanionAttackEvent args)
    {
        if (args.Handled || args.Target == uid) return;
        foreach (var pet in EntityQuery<MedievalCompanionComponent>().Where(c => c.Master == uid).ToArray())
            IssueOrder(pet.Owner, "attack", args.Target);
        args.Handled = true;
    }

    private void OnGuard(EntityUid uid, MedievalCompanionOwnerComponent comp, BookCompanionGuardEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(args.Target) || !HasComp<DamageableComponent>(args.Target)) return;
        foreach (var pet in EntityQuery<MedievalCompanionComponent>().Where(c => c.Master == uid && c.Owner != args.Target).ToArray())
        {
            IssueOrder(pet.Owner, "guard", args.Target);
            args.Handled = true;
        }
    }

    private void ClearGuardTarget(MedievalCompanionComponent pet)
    {
        if (pet.GuardTarget is not { } target) return;
        pet.GuardTarget = null;
        if (!TerminatingOrDeleted(target) && !EntityQuery<MedievalCompanionComponent>().Any(p => p.GuardTarget == target))
            RemComp<MedievalGuardedTargetComponent>(target);
    }

    private void OnCompanionShutdown(EntityUid uid, MedievalCompanionComponent comp, ComponentShutdown args) =>
        ClearGuardTarget(comp);

    private void OnGuardedTargetHurt(EntityUid uid, MedievalGuardedTargetComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } attacker || attacker == uid ||
            !HasComp<MobStateComponent>(attacker)) return;
        var origin = new EntityCoordinates(uid, Vector2.Zero);
        foreach (var pet in EntityQuery<MedievalCompanionComponent>().Where(p => p.GuardTarget == uid && p.Order == "guard").ToArray())
        {
            if (attacker == pet.Owner || attacker == pet.Master ||
                TryComp<MedievalCompanionComponent>(attacker, out var friendly) && friendly.Master == pet.Master ||
                !TryComp<HTNComponent>(pet.Owner, out var htn) || !htn.Enabled ||
                !origin.TryDistance(EntityManager, Transform(attacker).Coordinates, out var distance) || distance > 5f) continue;
            ResetPlan(htn);
            pet.CommandedEnemy = attacker;
            _factions.AggroEntity(pet.Owner, attacker);
            _npc.SetBlackboard(pet.Owner, NPCBlackboard.CurrentOrders, RatKingOrderType.CheeseEm);
            _npc.SetBlackboard(pet.Owner, NPCBlackboard.CurrentOrderedTarget, attacker);
            _npc.SetBlackboard(pet.Owner, NPCBlackboard.FollowTarget, origin);
            ResumeBrain(pet.Owner, htn, pet);
        }
    }

    public void Pacify(EntityUid beast, TimeSpan duration)
    {
        if (!TryComp<HTNComponent>(beast, out var htn) || HasComp<HumanoidAppearanceComponent>(beast) ||
            HasComp<BossComponent>(beast) || HasComp<MedievalUntameableComponent>(beast) ||
            TryComp<MindContainerComponent>(beast, out var mind) && mind.HasMind)
            return;
        if (!TryComp<MedievalPacifiedBeastComponent>(beast, out var calm))
        {
            calm = AddComp<MedievalPacifiedBeastComponent>(beast);
            calm.WasEnabled = htn.Enabled;
        }
        calm.Until = _timing.CurTime + duration;
        ResetPlan(htn);
        htn.Enabled = false;
    }

    private void OnHurt(EntityUid uid, MedievalPacifiedBeastComponent comp, DamageChangedEvent args)
    {
        // Training owns this pause and applies DoAfter's damage rule to both participants.
        if (HasComp<BookTamingLockComponent>(uid)) return;
        if (args.DamageIncreased) EndPacification(uid);
    }

    /// <summary>Ends training's quiet interval and, on failure, immediately makes the trainer a combat target.</summary>
    public void ReleaseTraining(EntityUid beast, EntityUid? attacker)
    {
        EndPacification(beast);
        if (attacker is not { } target || TerminatingOrDeleted(target) ||
            !TryComp<HTNComponent>(beast, out var htn) ||
            !TryComp<MobStateComponent>(beast, out var state) || state.CurrentState != MobState.Alive ||
            TryComp<MindContainerComponent>(beast, out var mind) && mind.HasMind)
            return;

        ResetPlan(htn);
        _factions.AggroEntity(beast, target);
        _npc.SetBlackboard(beast, NPCBlackboard.UtilityTarget, target);
        if (TryComp<NPCTargetMemoryComponent>(beast, out var memory))
        {
            memory.Target = target;
            memory.LastSeen = _timing.CurTime;
            memory.LastKnownCoordinates = Transform(target).Coordinates;
        }
        _htn.SetHTNEnabled((beast, htn), true);
        _htn.Replan(htn);
        EnsureComp<NPCMeleeCombatComponent>(beast).Target = target;
        _npc.WakeNPC(beast, htn);
    }

    private void EndPacification(EntityUid uid)
    {
        if (!TryComp<MedievalPacifiedBeastComponent>(uid, out var comp)) return;
        if (TryComp<HTNComponent>(uid, out var htn))
        {
            htn.Enabled = comp.WasEnabled;
            if (htn.Enabled) _htn.Replan(htn);
        }
        RemComp<MedievalPacifiedBeastComponent>(uid);
    }

    private void ResetPlan(HTNComponent htn)
    {
        _htn.SetHTNEnabled((htn.Owner, htn), false);
        _steering.Unregister(htn.Owner);
        if (TryComp<MedievalCompanionComponent>(htn.Owner, out var pet) && pet.CommandedEnemy is { } enemy)
        {
            _factions.DeAggroEntity(htn.Owner, enemy);
            pet.CommandedEnemy = null;
        }
        RemComp<NPCMeleeCombatComponent>(htn.Owner);
        RemComp<NPCRangedCombatComponent>(htn.Owner);
        if (TryComp<NPCTargetMemoryComponent>(htn.Owner, out var memory))
        {
            memory.Target = null;
            memory.LastKnownCoordinates = null;
        }
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.CurrentOrderedTarget);
        htn.Blackboard.Remove<EntityUid>(NPCBlackboard.UtilityTarget);
    }

    public override void Update(float frameTime)
    {
        foreach (var comp in EntityQuery<MedievalPacifiedBeastComponent>().ToArray())
            if (_timing.CurTime >= comp.Until && !HasComp<BookTamingLockComponent>(comp.Owner))
                EndPacification(comp.Owner);
        if (_timing.CurTime < _nextGuardReview) return;
        _nextGuardReview = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var guards = EntityQueryEnumerator<MedievalCompanionComponent, HTNComponent, TransformComponent>();
        while (guards.MoveNext(out var uid, out var pet, out var htn, out var xform))
        {
            if (pet.Order != "guard" || pet.GuardTarget is not { } target || !htn.Enabled) continue;
            if (TerminatingOrDeleted(target))
            {
                IssueOrder(uid, "follow");
                continue;
            }
            var origin = new EntityCoordinates(target, Vector2.Zero);
            if (!origin.TryDistance(EntityManager, xform.Coordinates, out var distance)) continue;
            var enemyGone = pet.CommandedEnemy is { } enemy &&
                (!TryComp<MobStateComponent>(enemy, out var state) || state.CurrentState == MobState.Dead ||
                 !origin.TryDistance(EntityManager, Transform(enemy).Coordinates, out var enemyDistance) || enemyDistance > 5f);
            if (pet.CommandedEnemy != null && (distance > 5f || enemyGone))
                IssueOrder(uid, "guard", target);
        }
    }
}
