using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Medieval.TempInvincibility;

[RegisterComponent]
public sealed partial class TempInvincibilityComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan EndTime = TimeSpan.Zero;
}
