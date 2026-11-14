namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireProjectilesSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-projectiles");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Projectiles &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Projectiles) == 0)
            args.Denied = true;
    }
}
