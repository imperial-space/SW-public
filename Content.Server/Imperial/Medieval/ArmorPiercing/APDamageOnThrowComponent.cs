using Content.Shared.Damage;
namespace Content.Server.Imperial.Medieval.APDamage;

[RegisterComponent]
public sealed partial class APDamageOnThrowComponent : Component
{
    [DataField(required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}

