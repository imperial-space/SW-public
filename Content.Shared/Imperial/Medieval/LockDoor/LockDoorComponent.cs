namespace Content.Shared.Imperial.LockDoor.Components;

[RegisterComponent]
public sealed partial class LockDoorComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public List<string> AccessLists = new();
}
