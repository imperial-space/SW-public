using System.Collections.Generic;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Factions.Components;

[RegisterComponent]
public sealed partial class MedievalFactionRelationsPendingOffersComponent : Component
{
    public List<MedievalFactionRelationsPendingOfferData> Offers = new();
}

public sealed class MedievalFactionRelationsPendingOfferData
{
    public ProtoId<MedievalFactionPrototype> UserFaction = string.Empty;
    public ProtoId<MedievalFactionPrototype> TargetFaction = string.Empty;
    public ProtoId<FactionRelationsPrototype> Relation = string.Empty;
    public EntityUid OfferedBy;
}
