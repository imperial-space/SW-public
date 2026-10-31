namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireFireSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-fire");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Fire &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Fire) == 0)
            args.Denied = true;
    }
}
