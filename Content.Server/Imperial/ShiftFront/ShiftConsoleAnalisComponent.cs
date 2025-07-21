using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;


namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftConsoleAnalisComponent : Component
{

    [DataField]
    public string Faction = "";

    [DataField]
    public bool Soljers = false;
}
