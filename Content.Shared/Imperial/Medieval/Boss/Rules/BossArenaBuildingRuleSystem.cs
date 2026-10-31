namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaBuildingRuleSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-building-denial");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.Building && !component.AllowBuilding)
            args.Denied = true;
    }
}
