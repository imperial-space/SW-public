namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireDirectSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-direct");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Direct &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Direct) == 0)
            args.Denied = true;
    }
}
