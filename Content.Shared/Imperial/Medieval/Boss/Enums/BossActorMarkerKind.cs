using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public enum BossActorMarkerKind
{
    Tank,
    Spread,
    Stack,
    TankBuster
}
