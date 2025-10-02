using Content.Shared.Examine;
using Content.Shared.Damage;

namespace Content.Server.DamageCheck;
public partial class DamageCheckSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageCheckableComponent, ExaminedEvent>(OnExamine);
    }
    private void OnExamine(EntityUid uid, DamageCheckableComponent comp, ExaminedEvent args)
    {
        // Bad shitcode for gates. Fix later
        if (!TryComp<DamageableComponent>(uid, out var damageable)) return;
        if (damageable.TotalDamage > 3600)
            args.PushMarkup(Loc.GetString("medieval-hm-damagecheck-almostbroke"));
        else if (damageable.TotalDamage > 2700)
            args.PushMarkup(Loc.GetString("medieval-hm-damagecheck-deep"));
        else if (damageable.TotalDamage > 1800)
            args.PushMarkup(Loc.GetString("medieval-hm-damagecheck-broken"));
        else if (damageable.TotalDamage > 900)
            args.PushMarkup(Loc.GetString("medieval-hm-damagecheck-ohno"));
        else if (damageable.TotalDamage > 220)
            args.PushMarkup(Loc.GetString("medieval-hm-damagecheck-notdamaged"));

    }

}
