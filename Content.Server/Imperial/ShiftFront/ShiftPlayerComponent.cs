using Robust.Shared.GameStates;

namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftPlayerComponent : Component
{
    [DataField]
    public string Faction = "";

    [DataField]
    public string Name = "";

    [DataField]
    public bool Leader = false;

    [DataField]
    public bool Eng = false;

    [DataField]
    public bool Ninja = false;

    [DataField]
    public float SuppressionMax = 110f;

    [DataField]
    public float SuppressionMin = 32f;

    [DataField]
    public float Suppression = 110f;

    [DataField]
    public float SuppressionRecovery = 5f;

    [DataField]
    public List<string> SuppressionPhrases = new();

}
