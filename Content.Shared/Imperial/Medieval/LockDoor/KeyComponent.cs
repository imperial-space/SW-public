namespace Content.Shared.Imperial.LockDoor.Components;

[RegisterComponent]
public sealed partial class KeyComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public List<string> Accesses = new();
}
