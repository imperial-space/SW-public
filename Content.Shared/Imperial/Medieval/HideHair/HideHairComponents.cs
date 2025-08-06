using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.HideHair.Components;

[RegisterComponent]
public sealed partial class HideHairComponent : Component
{
    [ViewVariables] // VVAccess.ReadOnly
    public EntityUid? Action;
    [ViewVariables(VVAccess.ReadWrite), DataField, ValidatePrototypeId<EntityPrototype>]
    public string PrototypeID = "HideHairAction";
}
public sealed partial class HideHairToggleEvent : InstantActionEvent { }
