using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Body.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Server.Interaction;
using Content.Server.Myrmex.Components;
using Content.Server.Stealth;
using Content.Server.Stunnable;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Myrmex;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Myrmex;

public sealed partial class MyrmexSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _formSys = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeedSys = default!;
    [Dependency] private readonly GunSystem _gunSys = default!;
    [Dependency] private readonly StunSystem _stunSys = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSys = default!;
    [Dependency] private readonly InteractionSystem _actSys = default!;
    [Dependency] private readonly StealthSystem _stealthSys = default!;
    [Dependency] private readonly ITimerManager _timerMan = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSys = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSys = default!;

    public void InitializeActions()
    {
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexShootEvent>(OnShoot);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexBoostEvent>(OnBoost);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleArmorEvent>(OnToggleArmor);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleStunEvent>(OnToggleStun);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexSpawnEvent>(OnSpawn);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexToggleStealthEvent>(OnToggleStealth);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexHealEvent>(OnHeal);

        SubscribeLocalEvent<MyrmexComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<MyrmexComponent, BeforeMeleeHitEvent>(OnBeforeHit);
        SubscribeLocalEvent<MyrmexComponent, ActionMyrmexSpawnDoAfterEvent>(OnSpawnDoAfter);
    }

    private void OnShoot(Entity<MyrmexComponent> ent, ref ActionMyrmexShootEvent args)
    {
        EntityUid projectileEnt = Spawn(args.ProjectileProto, _formSys.GetMapCoordinates(ent));
        Vector2 userPos = _formSys.GetWorldPosition(args.Performer);
        _gunSys.ShootProjectile(projectileEnt, _formSys.ToMapCoordinates(args.Target).Position - userPos, Vector2.Zero, null, args.Performer, args.Speed);

        args.Handled = true;
    }

    private void OnBoost(Entity<MyrmexComponent> ent, ref ActionMyrmexBoostEvent args)
    {
        float multiplier = args.Multiplier; //cause lambda is compaining about ref
        ModifyMoveSpeed(ent, multiplier);
        Timer timer = new((int)args.Duration.TotalMilliseconds, false, () =>
        {
            if (Exists(ent))
                ModifyMoveSpeed(ent, -multiplier);
        });
        _timerMan.AddTimer(timer);

        args.Handled = true;
    }

    private void OnToggleArmor(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleArmorEvent args)
    {
        ent.Comp.ArmorActive = !ent.Comp.ArmorActive;
        _damageable.SetDamageModifierSetId(ent, ent.Comp.ArmorActive ? ent.Comp.ActiveArmorProto : ent.Comp.StandardArmorProto);
        ModifyMoveSpeed(ent, ent.Comp.ArmorActive ? -ent.Comp.ActiveArmorSpeedMultiplier : ent.Comp.ActiveArmorSpeedMultiplier);

        args.Handled = true;
    }

    private void OnToggleStun(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleStunEvent args)
    {
        ent.Comp.StunActive = true;
        args.Handled = true;
    }

    private void OnSpawn(Entity<MyrmexComponent> ent, ref ActionMyrmexSpawnEvent args)
    {
        if (!_actSys.InRangeUnobstructed(ent, GetPosInfront(ent)))
            return;
        // imperial medieval - no args.Handled here, cooldown starts in OnSpawnDoAfter on success
        ActionMyrmexSpawnDoAfterEvent ev = new() { Proto = args.Proto, ActionUid = GetNetEntity(args.Action) };
        DoAfterArgs doAfterArgs = new(EntityManager, ent, args.DoAfterDuration, ev, ent) { BreakOnMove = true, BreakOnDamage = true };
        _doAfterSys.TryStartDoAfter(doAfterArgs);
    }

    private void OnToggleStealth(Entity<MyrmexComponent> ent, ref ActionMyrmexToggleStealthEvent args)
    {
        ent.Comp.StealthActive = !ent.Comp.StealthActive;
        _stealthSys.SetEnabled(ent, ent.Comp.StealthActive);
        args.Handled = true;
    }

    private void OnHeal(Entity<MyrmexComponent> ent, ref ActionMyrmexHealEvent args)
    {
        _damageable.TryChangeDamage(args.Target, args.HealedDamage, true);

        // imperial medieval - stop bleeding too, not just raw damage
        if (args.BleedReduction > 0f)
            _bloodstreamSys.TryModifyBleedAmount(args.Target, -args.BleedReduction);

        args.Handled = true;
    }

    //increases original movement speed by 'multiplier' percents (0-1)
    private void ModifyMoveSpeed(Entity<MyrmexComponent> ent, float multiplier)
    {
        MovementSpeedModifierComponent moveSpeed = EnsureComp<MovementSpeedModifierComponent>(ent);

        float oldMultiplier = ent.Comp.CurrentSpeedMultiplier;
        ent.Comp.CurrentSpeedMultiplier += multiplier;
        float realMultiplier = ent.Comp.CurrentSpeedMultiplier / oldMultiplier;

        _moveSpeedSys.ChangeBaseSpeed(
            ent,
            moveSpeed.BaseWalkSpeed * realMultiplier,
            moveSpeed.BaseSprintSpeed * realMultiplier,
            moveSpeed.BaseAcceleration * realMultiplier,
            moveSpeed);
    }

    // imperial medieval - reliable friendly-fire guard for BOTH light and wide (right-click) swings.
    // The wide swing's damage pass wasn't reliably cancelled by the AttackAttempt subscription, so
    // strip friendly myrmex out of the hit list here, before any damage or effect is applied.
    private void OnBeforeHit(Entity<MyrmexComponent> ent, ref BeforeMeleeHitEvent args)
    {
        args.HitEntities.RemoveAll(HasComp<MyrmexHungerComponent>);
    }

    private void OnHit(Entity<MyrmexComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.StunActive)
            return;

        ent.Comp.StunActive = false;

        // imperial medieval - HitEntities isnt friendly-fire filtered yet at this point, 
        // unlike the later damage pass, so check manually
        foreach (EntityUid hitEnt in args.HitEntities)
        {
            if(HasComp<MyrmexHungerComponent>(hitEnt))
               continue;

            // imperial medieval - real knockdown = full stamina wipe, ignoring the targets resist
            _staminaSys.TakeStaminaDamage(hitEnt, 9999f, ignoreResist: true);
            _stunSys.TryKnockdown(hitEnt, ent.Comp.StunDuration, true);
        }
    }

    private MapCoordinates GetPosInfront(EntityUid ent)
    {
        TransformComponent form = Transform(ent);
        Vector2 localPos = form.LocalPosition.Floored() + new Vector2(0.5f) + form.LocalRotation.GetCardinalDir().ToVec();
        return new MapCoordinates(Vector2.Transform(localPos, _formSys.GetWorldMatrix(form.ParentUid)), form.MapID);
    }

    private void OnSpawnDoAfter(Entity<MyrmexComponent> ent, ref ActionMyrmexSpawnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        MapCoordinates coords = GetPosInfront(ent);
        if (_actSys.InRangeUnobstructed(ent, coords))
        {
            Spawn(args.Proto, coords);
            // imperial medieval - cooldown starts on success. not on DoAfter start
            _actions.StartUseDelay(GetEntity(args.ActionUid));
            args.Handled = true;
        }
    }
}
