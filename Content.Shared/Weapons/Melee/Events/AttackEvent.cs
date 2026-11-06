using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events
{
    [Serializable, NetSerializable]
    public abstract class AttackEvent : EntityEventArgs
    {
        /// <summary>
        /// Coordinates being attacked.
        /// </summary>
        public readonly NetCoordinates Coordinates;

        protected AttackEvent(NetCoordinates coordinates)
        {
            Coordinates = coordinates;
        }
    }

    /// <summary>
    ///     Event raised on entities that have been attacked.
    /// </summary>
    public sealed class AttackedEvent : EntityEventArgs
    {
        /// <summary>
        ///     Entity used to attack, for broadcast purposes.
        /// </summary>
        public EntityUid Used { get; }

        /// <summary>
        ///     Entity that triggered the attack.
        /// </summary>
        public EntityUid User { get; }

        /// <summary>
        ///     The original location that was clicked by the user.
        /// </summary>
        public EntityCoordinates ClickLocation { get; }

        public DamageSpecifier BonusDamage = new();

        // Imperial medieval start
        /// <summary>For confirmed melee, preserve grip and charge poise from the original impact.</summary>
        public int? PoiseDamageOverride;
        // Imperial medieval end

        public AttackedEvent(EntityUid used, EntityUid user, EntityCoordinates clickLocation)
        {
            Used = used;
            User = user;
            ClickLocation = clickLocation;
        }
    }

    // Imperial medieval start
    /// <summary>Raised on each melee target after its health and stamina damage resolve.</summary>
    [ByRefEvent]
    public readonly record struct MeleeHitResolvedEvent(AttackedEvent Attack);
    // Imperial medieval end
}

