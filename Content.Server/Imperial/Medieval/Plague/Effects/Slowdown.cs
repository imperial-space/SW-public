
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class Slowdown : BasePlagueEffect
{
    [DataField(required: true)]
    public float Duration;

    [DataField]
    public bool Refresh = true;

    public override Slowdown CreateInstance()
    {
        return new Slowdown()
        {
            Delay = this.Delay,
            Other = this.Other,
            Duration = this.Duration,
            Refresh = this.Refresh
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var stun = entMan.System<StunSystem>();
        stun.TrySlowdown(uid, TimeSpan.FromSeconds(Duration), Refresh); ;
    }
}
