namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaEquipmentRuleSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-equipment-denial");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.Equipment && !component.AllowEquipment)
            args.Denied = true;
    }
}
