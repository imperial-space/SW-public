using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public enum BossBrazierState
{
    Empty,
    White,
    Purple,
    Victory
}
