
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage.Events;
using Content.Shared.Rejuvenate;

namespace Content.Shared.Imperial.Medieval.Additions;

public partial class ShieldOnStartupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _tick = default!;
    [Dependency] private readonly AlertsSystem _alert = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ShieldOnStartupComponent, ComponentStartup>(Init);
        SubscribeLocalEvent<ShieldOnStartupComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<ShieldOnStartupComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<ShieldOnStartupComponent, RejuvenateEvent>(OnRejuv);
        SubscribeLocalEvent<ShieldOnStartupComponent, ToggleCombatActionEvent>(OnCombatModeChanged);
    }

    public void OnRejuv(EntityUid uid, ShieldOnStartupComponent component, RejuvenateEvent args)
    {
        component.Spawned += TimeSpan.FromSeconds(5);
        _alert.ShowAlert(uid, "SpawnProtection", null, (_tick.CurTime, _tick.CurTime), true);
        RemComp<ShieldOnStartupComponent>(uid);
    }
    public void Init(EntityUid uid, ShieldOnStartupComponent component, ComponentStartup args)
    {
        if (!component.Enabled) return;
        component.Spawned = _tick.CurTime;
        _alert.ShowAlert(uid, "SpawnProtection", null, (_tick.CurTime, _tick.CurTime + TimeSpan.FromSeconds(5)), true);
    }
    private void OnBeforeDamageChanged(EntityUid uid, ShieldOnStartupComponent component, ref BeforeDamageChangedEvent args)
    {
        if (!component.Enabled) return;
        if (component.Spawned + TimeSpan.FromSeconds(5) < _tick.CurTime)
        {
            RemComp<ShieldOnStartupComponent>(uid);
            return;
        }
        args.Cancelled = true;
    }
    private void OnBeforeStaminaDamage(EntityUid uid, ShieldOnStartupComponent component, ref BeforeStaminaDamageEvent args)
    {
        if (!component.Enabled) return;
        if (component.Spawned + TimeSpan.FromSeconds(5) < _tick.CurTime)
        {
            RemComp<ShieldOnStartupComponent>(uid);
            return;
        }
        args.Cancelled = true;
    }

    private void OnCombatModeChanged(Entity<ShieldOnStartupComponent> ent, ref ToggleCombatActionEvent args)
    {
        _alert.ClearAlert(ent.Owner, "SpawnProtection");
        RemComp<ShieldOnStartupComponent>(ent);
    }
}
