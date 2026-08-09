using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Factions.Components;

[RegisterComponent]
public sealed partial class MedievalFactionFlagComponent : Component
{
    [DataField, ViewVariables]
    public ProtoId<MedievalFactionPrototype> Faction;
}

