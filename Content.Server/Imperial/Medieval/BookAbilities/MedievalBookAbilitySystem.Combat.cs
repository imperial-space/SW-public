using System.Numerics;
using System.Linq;
using Content.Shared.Blocking;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.BookAbilities;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Imperial.Medieval.Grab.Systems;
using Content.Shared.Imperial.Medieval.Knowledge;
using Content.Shared.MeleeParry;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Medieval.BookAbilities;

public sealed partial class MedievalBookAbilitySystem
{
    [Dependency] private readonly MeleeParrySystem _parry = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly GrabSystem _grab = default!;
    [Dependency] private readonly BlockingSystem _blocking = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    private readonly Dictionary<EntityUid, (EntityUid Attacker, TimeSpan Until)> _ripostes = new();
    private readonly Dictionary<EntityUid, TimeSpan> _guardBreak = new();
    private readonly Dictionary<EntityUid, TimeSpan> _piercingShot = new();

    private void InitializeCombat()
    {
        SubscribeLocalEvent<LearnedKnowledgeComponent, MeleeParrySucceededEvent>(OnParry);
        SubscribeLocalEvent<LearnedKnowledgeComponent, ProjectileReflectAttemptEvent>(OnProjectileParry);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookBreakGuardActionEvent>(OnPrepareGuardBreak);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookPiercingShotActionEvent>(OnPreparePiercing);
        SubscribeLocalEvent<LearnedKnowledgeComponent, BookEscapeGrabActionEvent>(OnEscapeGrab);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnPiercingShot);
        SubscribeLocalEvent<BookPiercingProjectileComponent, ProjectileBeforeHitEvent>(OnPiercingHit);
        SubscribeLocalEvent<BookPiercingProjectileComponent, PreventCollideEvent>(OnPiercingCollision);
    }

    private void OnParry(EntityUid uid, LearnedKnowledgeComponent comp, ref MeleeParrySucceededEvent args)
    {
        if (!Knows(uid, "BookDisarmingRiposte")) return;
        _ripostes[uid] = (args.Attacker, _timing.CurTime + TimeSpan.FromSeconds(2));
        _popup.PopupEntity(Loc.GetString("book-ability-riposte-ready"), uid, uid);
    }

    private void OnMeleeHit(EntityUid uid, MeleeWeaponComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0) return;
        if (_ripostes.TryGetValue(args.User, out var riposte) && riposte.Until >= _timing.CurTime &&
            args.HitEntities.Contains(riposte.Attacker) && Knows(args.User, "BookDisarmingRiposte"))
        {
            _ripostes.Remove(args.User);
            if (_hands.GetActiveItem(riposte.Attacker) is { } held)
                _hands.TryDrop(riposte.Attacker, held);
        }
        if (args.Direction == null || !_guardBreak.Remove(args.User, out var until) ||
            until < _timing.CurTime || !Knows(args.User, "BookBreakGuard")) return;
        foreach (var target in args.HitEntities)
        {
            if (!TryComp<BlockingUserComponent>(target, out var user) ||
                user.BlockingItem is not { } shield || !TryComp<BlockingComponent>(shield, out var blocking)) continue;
            _blocking.StopBlocking(shield, blocking, target);
            var broken = EnsureComp<BookBrokenGuardComponent>(shield);
            broken.Until = _timing.CurTime + TimeSpan.FromSeconds(5);
            Dirty(shield, broken);
        }
    }

    private void OnPrepareGuardBreak(EntityUid uid, LearnedKnowledgeComponent comp, BookBreakGuardActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookBreakGuard") || _hands.GetActiveItem(uid) is not { } weapon ||
            !HasComp<MeleeWeaponComponent>(weapon)) return;
        _guardBreak[uid] = _timing.CurTime + TimeSpan.FromSeconds(10);
        _popup.PopupEntity(Loc.GetString("book-ability-guard-ready"), uid, uid);
        args.Handled = true;
    }

    private void OnPreparePiercing(EntityUid uid, LearnedKnowledgeComponent comp, BookPiercingShotActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookPiercingShot")) return;
        _piercingShot[uid] = _timing.CurTime + TimeSpan.FromSeconds(20);
        _popup.PopupEntity(Loc.GetString("book-ability-piercing-ready"), uid, uid);
        args.Handled = true;
    }

    private void OnPiercingShot(EntityUid uid, GunComponent comp, AmmoShotEvent args)
    {
        // Embedded arrows can be retrieved and shot again; a new ordinary shot must not retain its old victim.
        foreach (var projectile in args.FiredProjectiles)
            RemComp<BookPiercingProjectileComponent>(projectile);
        if (MetaData(uid).EntityPrototype?.ID.Contains("Crossbow", StringComparison.OrdinalIgnoreCase) != true) return;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out var shot) || shot.Shooter is not { } shooter ||
                !_piercingShot.TryGetValue(shooter, out var until) || until < _timing.CurTime ||
                !Knows(shooter, "BookPiercingShot")) continue;
            _piercingShot.Remove(shooter);
            EnsureComp<BookPiercingProjectileComponent>(projectile).Hit.Clear();
            break;
        }
    }

    private void OnPiercingHit(EntityUid uid, BookPiercingProjectileComponent pierce, ref ProjectileBeforeHitEvent args)
    {
        if (args.Cancelled || !TryComp<ProjectileComponent>(uid, out var comp) || comp.ProjectileSpent) return;
        if (pierce.Hit.Contains(args.Target)) { args.Cancelled = true; return; }
        // A first living target is pierced. Walls and the second creature retain normal impact/embedding.
        if (pierce.Hit.Count != 0 || !HasComp<MobStateComponent>(args.Target)) return;
        pierce.Hit.Add(args.Target);
        _damage.TryChangeDamage(args.Target, comp.Damage * _damage.UniversalProjectileDamageModifier,
            comp.IgnoreResistances, origin: comp.Shooter);
        args.Cancelled = true;
    }

    private void OnPiercingCollision(EntityUid uid, BookPiercingProjectileComponent comp, ref PreventCollideEvent args)
    {
        if (comp.Hit.Contains(args.OtherEntity)) args.Cancelled = true;
    }

    private void OnProjectileParry(EntityUid uid, LearnedKnowledgeComponent comp, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled || !Knows(uid, "BookParryProjectile") || !HasComp<EmbeddableProjectileComponent>(args.ProjUid) ||
            !_parry.CheckParryable(uid, 1f, out var weapon, out var parry, out _, out _)) return;
        args.Cancelled = true;
        args.Component.ProjectileSpent = true;
        Dirty(args.ProjUid, args.Component);
        _physics.SetLinearVelocity(args.ProjUid, Vector2.Zero);
        parry.ParriedTime = TimeSpan.Zero;
        parry.LastParryTime = TimeSpan.Zero;
        Dirty(weapon, parry);
        Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);
    }

    private void OnEscapeGrab(EntityUid uid, LearnedKnowledgeComponent comp, BookEscapeGrabActionEvent args)
    {
        if (args.Handled || !Knows(uid, "BookEscapeGrab") || !TryComp<GrabbableComponent>(uid, out var grabbed) ||
            grabbed.Grabber is not { } attacker) return;
        const float cost = 25f;
        if (TryComp<StaminaComponent>(uid, out var stamina) &&
            (stamina.Critical || _stamina.GetStaminaDamage(uid, stamina) + cost >= stamina.CritThreshold))
        {
            _popup.PopupEntity(Loc.GetString("book-ability-exhausted"), uid, uid);
            return;
        }
        if (!_grab.TryStopGrab(uid, grabbed)) return;
        _stamina.TakeStaminaDamage(uid, cost, stamina, visual: false, ignoreResist: true);
        var away = _transform.GetWorldPosition(uid) - _transform.GetWorldPosition(attacker);
        if (away.LengthSquared() > 0.001f)
            _physics.SetLinearVelocity(uid, Vector2.Normalize(away) * 5);
        args.Handled = true;
    }
}
