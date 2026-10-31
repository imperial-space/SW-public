using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeSwingComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<MeleeSwingStylePrototype> Style;

    [DataField, AutoNetworkedField]
    public bool RequireWielded;                                                  // requires both hands before charging

    [DataField, AutoNetworkedField]
    public float MaximumSwingScale = 1f;                                         // reach multiplier at full charge

    [DataField, AutoNetworkedField]
    public bool NextSwingLeftToRight = true;                                     // direction used by the next successful swing
}
