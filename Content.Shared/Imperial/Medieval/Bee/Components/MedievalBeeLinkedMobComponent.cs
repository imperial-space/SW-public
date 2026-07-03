using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeLinkedMobComponent : Component
{
    public Entity<MedievalBeeLinkedSpawnerComponent>? LinkedSpawner;
}
