using Content.Shared.Dataset;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeHiveComponent : Component
{
    public Entity<MedievalBeeGridComponent> Grid;
    [DataField("gridDataset"), ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<DatasetPrototype> GridDataset = "MBeeGrids";
    public bool Pacified = false;
    public TimeSpan? PacifyEnd;
    public List<Entity<MedievalBeeComponent>> Bees = new();
    public TimeSpan? PacifyCooldown;
}
