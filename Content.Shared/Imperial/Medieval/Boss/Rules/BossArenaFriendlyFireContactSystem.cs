namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireContactSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-contact");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Contact &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Contact) == 0)
            args.Denied = true;
    }
}
