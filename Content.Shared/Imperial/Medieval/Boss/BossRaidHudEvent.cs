using Content.Shared.Mobs;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public sealed class BossRaidHudEvent(NetEntity? arena, List<BossRaidMember> members) : EntityEventArgs
{
    public readonly NetEntity? Arena = arena;
    public readonly List<BossRaidMember> Members = members;
}

[Serializable, NetSerializable]
public sealed record BossRaidMember(NetEntity Entity, string Name, float Health, float MaxHealth,
    float Energy, float MaxEnergy, float Mana, float MaxMana, MobState State, bool Connected);
