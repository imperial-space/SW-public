using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftBuildLightComponent : Component
{
    [DataField]
    public string BuildingCode = "";
}
