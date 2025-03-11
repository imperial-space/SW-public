using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.DurabilityDisplay.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DurabilityDisplayComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true), AutoNetworkedField]
    public string Dub = "Full";
}
