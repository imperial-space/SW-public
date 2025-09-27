using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Dash;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

public abstract partial class SharedLycantropySystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WerewolfMoonRageComponent, MapInitEvent>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfMoonRageComponent, ComponentShutdown>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfHotBloodComponent, MapInitEvent>(RefreshMoveSpeed);
        SubscribeLocalEvent<WerewolfHotBloodComponent, ComponentShutdown>(RefreshMoveSpeed);

        SubscribeLocalEvent<WerewolfMoonRageComponent, RefreshMovementSpeedModifiersEvent>(OnMoonSpeedModify);
        SubscribeLocalEvent<WerewolfHotBloodComponent, RefreshMovementSpeedModifiersEvent>(OnHotBloodSpeedModify);
        SubscribeLocalEvent<WerewolfBloodHuntComponent, CheckDashStaminaCostModifiersEvent>(OnBloodHuntDash);
        SubscribeLocalEvent<WerewolfBloodHuntComponent, CheckDashCooldownModifiersEvent>(OnBloodHuntDashDelay);
        SubscribeLocalEvent<WerewolfShadowDashComponent, StartCollideEvent>(OnSDashCollide);
    }

    private void RefreshMoveSpeed(EntityUid uid, Component comp, EntityEventArgs args)
        => _moveSpeed.RefreshMovementSpeedModifiers(uid);

    private void OnMoonSpeedModify(EntityUid uid, WerewolfMoonRageComponent comp, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(comp.Modifier);

    private void OnHotBloodSpeedModify(EntityUid uid, WerewolfHotBloodComponent comp, RefreshMovementSpeedModifiersEvent args)
        => args.ModifySpeed(comp.Modifier);

    private void OnBloodHuntDash(EntityUid uid, WerewolfBloodHuntComponent comp, ref CheckDashStaminaCostModifiersEvent args)
    {
        args.Modifier *= 0;
    }

    private void OnBloodHuntDashDelay(EntityUid uid, WerewolfBloodHuntComponent comp, ref CheckDashCooldownModifiersEvent args)
    {
        args.Modifier *= 0;
    }

    private void OnSDashCollide(EntityUid uid, WerewolfShadowDashComponent comp, ref StartCollideEvent args)
    {
        if (!HasComp<StaminaComponent>(args.OtherEntity))
            return;

        _stamina.TakeStaminaDamage(args.OtherEntity, 200, source: uid, ignoreResist: true);
        RemComp<WerewolfShadowDashComponent>(uid);
    }
}
