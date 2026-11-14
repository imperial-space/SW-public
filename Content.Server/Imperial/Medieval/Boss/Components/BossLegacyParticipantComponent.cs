using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossLegacyParticipantComponent : Component
{
    [ViewVariables]
    public EntityUid Boss;

    [ViewVariables]
    public EntityCoordinates? ReturnCoordinates;
}
