using Content.Shared.Imperial.Medieval.Boss;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossFocusMarkerComponent : Component
{
    public bool Started;
    public EntityUid? Tank;
    public EntityUid? Marker;
}
