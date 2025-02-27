using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.MedievalItemRustComponent;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalItemRustComponent : Component
{
    [DataField, AutoNetworkedField]
    public float RustPercentage = 0.0f;

    [DataField, AutoNetworkedField]
    public Color RustColor = Color.FromHex("#892F02");

    [ViewVariables]
    public float Seed = 0.0f;
}
