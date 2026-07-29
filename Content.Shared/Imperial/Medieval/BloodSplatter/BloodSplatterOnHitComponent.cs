using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.BloodSplatter;

[RegisterComponent]
public sealed partial class BloodSplatterOnHitComponent : Component
{
    [DataField]
    public List<EntProtoId> Effects = new()
    {
        "MedievalBloodSplatterEffect1",
        "MedievalBloodSplatterEffect2",
        "MedievalBloodSplatterEffect3",
        "MedievalBloodSplatterEffect4",
    };

    public EntityUid? PendingAttacker;
}
