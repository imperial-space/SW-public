namespace Content.Shared.Imperial.Medieval.Actions;

[RegisterComponent]
public sealed partial class AddActionOnUseComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public string ActionId = "idkreally";
}
