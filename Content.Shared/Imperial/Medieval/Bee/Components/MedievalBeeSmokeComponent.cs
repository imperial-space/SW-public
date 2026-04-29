using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeSmokeComponent : Component
{
    [DataField]
    public TimeSpan PacifyTime = TimeSpan.FromSeconds(120);
    [DataField("uses")]
    public int UsesLeft = 3;
    [DataField("maxUses")]
    public int MaxUses = 3;
    [DataField("resourceStack")]
    public ProtoId<StackPrototype> ResourceStack = "MedievalMagicDust";

}
