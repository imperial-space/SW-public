namespace Content.Shared.Imperial.Medieval.Boss;

public enum BossArenaRule : byte
{
    FriendlyDamage,
    Control,
    Building,
    Equipment,
    Armor,
}

[ByRefEvent]
public struct BossArenaRuleEvent(BossArenaRulesComponent settings, BossArenaRule rule, BossArenaDamageKind damageKind = BossArenaDamageKind.Direct)
{
    public BossArenaRulesComponent Settings = settings;
    public BossArenaRule Rule = rule;
    public BossArenaDamageKind DamageKind = damageKind;
    public bool Denied;
}
