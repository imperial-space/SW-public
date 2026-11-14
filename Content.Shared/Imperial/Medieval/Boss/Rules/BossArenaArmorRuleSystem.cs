namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaArmorRuleSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-armor-protection");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.Armor && !component.AllowArmorWear)
            args.Denied = true;
    }
}
