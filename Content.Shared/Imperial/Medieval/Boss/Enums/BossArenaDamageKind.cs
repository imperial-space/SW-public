using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Flags, Serializable, NetSerializable]
public enum BossArenaDamageKind : byte
{
    None = 0,
    Direct = 64,
    Spells = 1,
    Melee = 2,
    Projectiles = 4,
    Explosions = 8,
    Contact = 16,
    Fire = 32,
}
