namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeGridComponent : Component
{
    public EntityUid? Hive;
    public List<Entity<MedievalBeePlayerSpawnComponent>> Spawns = new();
}
