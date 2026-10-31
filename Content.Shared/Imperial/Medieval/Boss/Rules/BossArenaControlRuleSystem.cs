namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaControlRuleSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-control-denial");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.Control && !component.AllowControl)
            args.Denied = true;
    }
}
