using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public enum BossTelegraphPattern
{
    Sinistra,
    Dextra,
    Frons,
    Tergum,
    BlackoutNorthWest,
    BlackoutNorthEast,
    BlackoutSouthWest,
    BlackoutSouthEast,
    Rejection,
    Invitation,
    Cross,
    Divide,
    Spread,
    Stack,
    BlueFirst,
    PurpleFirst,
    CardinalCone,
    CrystalRain,
    TravelOnly,
    CrystalRainSmall
}
