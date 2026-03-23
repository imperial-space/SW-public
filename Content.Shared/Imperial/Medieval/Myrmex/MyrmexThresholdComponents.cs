using Content.Shared.FixedPoint;
using Content.Shared.Mobs;

namespace Content.Shared.Imperial.Medieval.Myrmex;

[RegisterComponent]
public sealed partial class MyrmexThresholdComponent : Component
{
    [DataField]
    public List<(FixedPoint2 threshold, MobState state)>? BaseHealthThresholds = null;
}
