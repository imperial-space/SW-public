namespace Content.Shared.Imperial.Medieval.Ships.Sea;

/// <summary>
/// Компонент моря
/// </summary>
[RegisterComponent]
public sealed partial class SeaComponent : Component
{
    [DataField("Disabled")]
    public bool Disabled;
}
