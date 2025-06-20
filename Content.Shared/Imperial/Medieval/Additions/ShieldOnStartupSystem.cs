
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage.Events;

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
    }
    public void Init(EntityUid uid, ShieldOnStartupComponent component, ComponentStartup args)
    {
        component.Spawned = _tick.CurTime;
        _alert.ShowAlert(uid, "SpawnProtection", null, (_tick.CurTime, _tick.CurTime + TimeSpan.FromSeconds(90)), true);
    }
    private void OnBeforeDamageChanged(EntityUid uid, ShieldOnStartupComponent component, ref BeforeDamageChangedEvent args)
    {
        if (component.Spawned + TimeSpan.FromSeconds(90) < _tick.CurTime)
            return;
        args.Cancelled = true;
    }
    private void OnBeforeStaminaDamage(EntityUid uid, ShieldOnStartupComponent component, ref BeforeStaminaDamageEvent args)
    {
        if (component.Spawned + TimeSpan.FromSeconds(90) < _tick.CurTime)
            return;
        args.Cancelled = true;
    }
}
