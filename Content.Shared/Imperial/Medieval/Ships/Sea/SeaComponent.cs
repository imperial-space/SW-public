using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Ships.Sea;

/// <summary>
/// Компонент моря
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeaComponent : Component
{
    [DataField("Disabled"), AutoNetworkedField]
    public bool Disabled;
}
