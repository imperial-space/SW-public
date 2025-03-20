using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EntityLayer;


/// <summary>
/// If you want to add a new layer, just add it here. Values ​​should NOT be repeated. Use ONLY the bitwise shift operator.
/// </summary>
[Serializable, NetSerializable]
public enum EntityLayerGroups
{
    None = 0,
    PhaseSpace = 1 << 0,

    All = PhaseSpace,
    AllWithOverworld = All | 1 << 31 // 1 << 31 is 2,147,483,647 - max int value. (Maybe use uint or ulong?)
}
