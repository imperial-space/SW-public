using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Items;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemNameColorComponent : Component
{
    [DataField, AutoNetworkedField] public Color Color = Color.White;
    [DataField, AutoNetworkedField] public Color PulseColor = Color.White;
    [DataField, AutoNetworkedField] public float PulseSeconds;
}
