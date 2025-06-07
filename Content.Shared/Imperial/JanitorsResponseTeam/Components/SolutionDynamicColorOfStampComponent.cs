namespace Content.Shared.Imperial.JanitorsResponseTeam.Components;

[RegisterComponent]
public sealed partial class SolutionDynamicColorOfStampComponent : Component
{
    /// <summary>
    /// Changed color
    /// </summary>
    [DataField]
    public Color Color;

    /// <summary>
    /// Do I need to check who put the seal on (profession)
    /// </summary>
    [DataField]
    public bool CheckValidRole = false;

    /// <summary>
    /// What role (profession) should be stamped
    /// </summary>
    [DataField]
    public string RoleName = "";

    /// <summary>
    /// False seal name
    /// </summary>
    [DataField]
    public string FalseStampedName = "";
}
