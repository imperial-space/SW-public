namespace Content.Shared.Imperial.WhitelistClothing.Components;

[RegisterComponent]
public sealed partial class WhitelistClothingComponent : Component
{
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string Whitelist = "goblin_armor";
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Slot = "outerclothing";
}
