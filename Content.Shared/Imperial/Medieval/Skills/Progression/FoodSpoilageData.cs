using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Distinguishes natural spoilage from poison injected into the same food.</summary>
[Serializable, NetSerializable]
public sealed partial class FoodSpoilageData : ReagentData
{
    public override ReagentData Clone() => this;
    public override bool Equals(ReagentData? other) => other is FoodSpoilageData;
    public override int GetHashCode() => typeof(FoodSpoilageData).GetHashCode();
}
