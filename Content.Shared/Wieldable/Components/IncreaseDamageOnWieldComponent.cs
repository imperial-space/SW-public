using Content.Shared.Damage;

namespace Content.Shared.Wieldable.Components;

// Imperial medieval start
[RegisterComponent, Access(typeof(SharedWieldableSystem), Other = AccessPermissions.ReadExecute)]
// Imperial medieval end
public sealed partial class IncreaseDamageOnWieldComponent : Component
{
    [DataField("damage", required: true)]
    public DamageSpecifier BonusDamage = default!;
}

