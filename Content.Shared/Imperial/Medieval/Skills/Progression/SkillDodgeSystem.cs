using Content.Shared.Damage;
using System.Linq;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Skills;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillDodgedProjectileComponent : Component
{
    // The same projectile must pass through its dodger, not hit them on the next physics tick.
    [DataField, AutoNetworkedField] public HashSet<EntityUid> Targets = new();
}

public sealed class SkillDodgeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkillsComponent, BeforeAttackEffectsEvent>(OnIncoming);
        SubscribeLocalEvent<SkillsComponent, PreventCollideEvent>(OnCollision);
        SubscribeLocalEvent<SkillDodgedProjectileComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<SkillDodgedProjectileComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnThrown(EntityUid uid, SkillDodgedProjectileComponent dodged, ref ThrownEvent args) => ResetFlight(uid, dodged);
    private void OnStopThrow(EntityUid uid, SkillDodgedProjectileComponent dodged, ref StopThrowEvent args) => ResetFlight(uid, dodged);

    private void ResetFlight(EntityUid uid, SkillDodgedProjectileComponent dodged)
    {
        // Immunity belongs to this flight, never to all future throws of the same item.
        if (dodged.Targets.Count == 0)
            return;
        dodged.Targets.Clear();
        Dirty(uid, dodged);
        _physics.RegenerateContacts(uid);
    }

    private void OnCollision(EntityUid uid, SkillsComponent skills, ref PreventCollideEvent args)
    {
        // This event runs during broad-phase filtering, before an actual hit is known.
        // It may suppress an already dodged projectile, but must never spend a charge.
        if (TryComp<SkillDodgedProjectileComponent>(args.OtherEntity, out var dodged) && dodged.Targets.Contains(uid))
            args.Cancelled = true;
    }

    private bool DodgeContact(EntityUid weapon, EntityUid target, SkillsComponent skills)
    {
        EntityUid? attacker;
        if (TryComp<ProjectileComponent>(weapon, out var projectile))
        {
            if (projectile.ProjectileSpent || projectile.IgnoreShooter && projectile.Shooter == target
                || projectile.OnlyCollideWhenShot && projectile.Weapon == null)
                return false;
            attacker = projectile.Shooter;
        }
        else if (TryComp<ThrownItemComponent>(weapon, out var thrown))
        {
            if (TryComp<SkillProgressionComponent>(weapon, out var jumping) && jumping.JumpUntil > _timing.CurTime)
                return false;
            attacker = thrown.Thrower;
        }
        else
            return false;
        return TryDodge(target, skills, weapon, attacker, true);
    }

    private void OnIncoming(EntityUid uid, SkillsComponent skills, ref BeforeAttackEffectsEvent args)
    {
        if (!args.Cancelled)
            args.Cancelled = args.Delivery == AttackDelivery.Melee
                ? TryDodge(uid, skills, args.Weapon, args.Attacker, false)
                : DodgeContact(args.Weapon, uid, skills);
    }

    private bool TryDodge(EntityUid uid, SkillsComponent skills, EntityUid weapon, EntityUid? attacker, bool projectile)
    {
        if (projectile && TryComp<SkillDodgedProjectileComponent>(weapon, out var previous) && previous.Targets.Contains(uid))
            return true;
        if (attacker == uid || _mobs.IsDead(uid) || SkillScaling.Level(skills, SharedSkillsSystem.AgilityId) < SkillScaling.Master)
            return false;
        var state = EnsureComp<SkillProgressionComponent>(uid);
        if (_timing.CurTime < state.NextDodge)
            return false;
        state.NextDodge = _timing.CurTime + TimeSpan.FromSeconds(
            _prototypes.Index<SkillPrototype>(SharedSkillsSystem.AgilityId).Modifiers["DodgeCooldown"]);
        Dirty(uid, state);
        if (projectile)
        {
            var dodged = EnsureComp<SkillDodgedProjectileComponent>(weapon);
            dodged.Targets.Add(uid);
            Dirty(weapon, dodged);
            // StartCollide runs before the solver. Remove this actual contact now, so even
            // a hard thrown object cannot push the dodger before the next filtering pass.
            if (TryComp<FixturesComponent>(weapon, out var fixtures))
                foreach (var contact in fixtures.Fixtures.Values.SelectMany(x => x.Contacts.Values).Distinct().ToArray())
                    if (contact.EntityA == uid || contact.EntityB == uid)
                        _physics.DestroyContact(contact);
        }
        _popup.PopupPredicted(Loc.GetString("skills-dodged-hit"), uid, uid);
        return true;
    }
}
