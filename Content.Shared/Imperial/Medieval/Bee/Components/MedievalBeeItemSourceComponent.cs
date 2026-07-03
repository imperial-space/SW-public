using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeItemSourceComponent : Component
{
    public TimeSpan NextGather = TimeSpan.Zero;
    [DataField("cooldown")]
    public TimeSpan GatherCooldown = TimeSpan.FromSeconds(30);
    [DataField]
    public EntProtoId Item;

}
