using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeComponent : Component
{
    public Entity<MedievalBeeHiveComponent>? ConnectedHive;
    public ProtoId<NpcFactionPrototype> HostileFaction = "Xeno";
    public ProtoId<NpcFactionPrototype> FriendlyFaction = "Nanotrasen";
}
