using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Damage.Systems;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class ChangeDamage : BasePlagueEffect
{
    [DataField("damage", required: true)]
    public DamageSpecifier DealtDamage;

    public override ChangeDamage CreateInstance()
    {
        return new ChangeDamage()
        {
            Delay = this.Delay,
            Other = this.Other,
            DealtDamage = this.DealtDamage
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var damageable = entMan.System<DamageableSystem>();
        damageable.TryChangeDamage(uid, DealtDamage, true);
    }
}
