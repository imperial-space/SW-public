using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Boss;

/// <summary>
/// </summary>
[RegisterComponent]
public sealed partial class BossDamageOnContactComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage;
}
