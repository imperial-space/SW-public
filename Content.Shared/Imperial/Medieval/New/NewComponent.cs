namespace Content.Shared.Imperial.Medieval.New;

[RegisterComponent]
public sealed partial class NewComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField(required: true)]
    public string ActionId = "idkreally";
}
