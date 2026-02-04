namespace Content.Server.Imperial.Medieval.Cult.Bloodspells;

[RegisterComponent]
public sealed partial class MedievalBloodedComponent : Component
{
    [DataField("blood"), ViewVariables(VVAccess.ReadWrite)]
    public int Blood;
}
