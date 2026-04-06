using Content.Shared.Damage;

namespace Content.Server.Imperial.Medieval.Courier;

[RegisterComponent, ComponentProtoName("veryFragile")]
public sealed partial class VeryFragileComponent : Component
{
    [ViewVariables]
    public EntityUid? Carrier;

    [DataField]
    public DamageSpecifier StaminaCritDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 100 },
        },
    };
}
