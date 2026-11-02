using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Medieval.Lycantropy;

[DataDefinition]
public sealed partial class SetWerewolfDamageEvent : EntityEventArgs
{
    [DataField]
    public Dictionary<string, FixedPoint2> Replacements = new();

    // optional poise damage override for claw upgrades.
    [DataField]
    public int? PoiseDamage;
}
