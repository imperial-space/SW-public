using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeSwingComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<MeleeSwingStylePrototype> Style;

    [DataField, AutoNetworkedField]
    public bool RequireWielded;

    [DataField, AutoNetworkedField]
    public float MaximumSwingScale = 1f;

    [DataField, AutoNetworkedField]
    public bool NextSwingLeftToRight = true;
}
