using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftConverterComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public int TimeTillNextGen = 25;

    [DataField]
    public int OverallGenTime = 25;

    [DataField]
    public bool PoToBio = true;

    [DataField]
    public bool Enabled = false;

}
