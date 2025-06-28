using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class EldritchInfluenceDrainerComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan TimeModifier = TimeSpan.FromSeconds(0.5);

    [DataField]
    public bool Hidden;
}
