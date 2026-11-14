namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireMeleeSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-melee");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Melee &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Melee) == 0)
            args.Denied = true;
    }
}
