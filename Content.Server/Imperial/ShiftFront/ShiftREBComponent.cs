using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftREBComponent : Component
{
    [DataField]
    public string Faction = "";
    [DataField]
    public bool Enabled = true;

    [DataField]
    public bool RequiredEquip = false;

    [DataField]
    public float Radius = 10f;

    [DataField]
    public int MinFreq = 2200;

    [DataField]
    public int CurFreq = 0;

    [DataField]
    public int MaxFreq = 5800;

    [DataField]
    public int FreqRadius = 850;

}
