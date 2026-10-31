using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Boss;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BossArenaRulesComponent : Component
{
    [DataField, AutoNetworkedField]
    public BossArenaDamageKind AllowedFriendlyDamage;                            // damage types allowed by installed rules
    [DataField, AutoNetworkedField]
    public bool AllowControl;
    [DataField, AutoNetworkedField]
    public bool AllowBuilding;                                                   // allows arena building when its rule is installed
    [DataField, AutoNetworkedField]
    public bool AllowEquipment;
    [DataField, AutoNetworkedField]
    public bool AllowArmorWear;                                                  // allows boss attacks to wear down armor
}
