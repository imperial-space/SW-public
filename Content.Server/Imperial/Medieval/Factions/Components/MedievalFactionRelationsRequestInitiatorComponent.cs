using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Factions.Components;

[RegisterComponent]
public sealed partial class MedievalFactionRelationsRequestInitiatorComponent : Component
{
    public EntityUid RequestedBy;
}
