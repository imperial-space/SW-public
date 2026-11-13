// reset per-hit anti-duplicate state on each manual weapon throw.
using Content.Shared.Imperial.Medieval.Poise;
using Content.Shared.Throwing;

namespace Content.Server.Imperial.Medieval.Poise;

public sealed class ThrownWeaponPoiseResetSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrownWeaponPoiseResetComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(EntityUid uid, ThrownWeaponPoiseResetComponent comp, ref ThrownEvent args)
    {
        if (TryComp<PoiseImpactComponent>(uid, out var impact))
            impact.HitTargets.Clear();
    }
}
