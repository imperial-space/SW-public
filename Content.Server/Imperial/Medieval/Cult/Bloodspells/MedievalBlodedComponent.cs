namespace Content.Server.Imperial.Medieval.Cult.Bloodspells;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class MedievalBlodedComponent : Component
{
    [DataField("blood")]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Blood;
}
