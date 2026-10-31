namespace Content.Shared.Imperial.Medieval.Boss;

public sealed class BossArenaFriendlyFireSpellsSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-friendly-fire-spells");
        SubscribeLocalEvent<BossArenaRuleEvent>(OnRule);
    }

    private void OnRule(ref BossArenaRuleEvent args)
    {
        var component = args.Settings;
        if (args.Rule == BossArenaRule.FriendlyDamage && args.DamageKind == BossArenaDamageKind.Spells &&
            (component.AllowedFriendlyDamage & BossArenaDamageKind.Spells) == 0)
            args.Denied = true;
    }
}
