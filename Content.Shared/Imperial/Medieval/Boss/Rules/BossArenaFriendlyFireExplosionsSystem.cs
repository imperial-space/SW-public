namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireExplosionsSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-explosions");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Explosions &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Explosions) == 0)
            args.Denied = true;
    }
}
