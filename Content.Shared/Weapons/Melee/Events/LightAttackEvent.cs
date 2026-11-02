// Imperial medieval edit - optional input identity for predicted advanced light attacks.
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events;

/// <summary>
/// Raised when a light attack is made.
/// </summary>
[Serializable, NetSerializable]
public sealed class LightAttackEvent : AttackEvent
{
    public readonly NetEntity? Target;
    public readonly NetEntity Weapon;
    public readonly uint AttackId; // Imperial medieval edit

    public LightAttackEvent(NetEntity? target, NetEntity weapon, NetCoordinates coordinates, uint attackId = 0) : base(coordinates)
    {
        Target = target;
        Weapon = weapon;
        AttackId = attackId; // Imperial medieval edit
    }
}
