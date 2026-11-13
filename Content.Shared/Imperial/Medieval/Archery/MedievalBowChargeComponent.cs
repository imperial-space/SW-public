
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Archery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class MedievalBowChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? DrawSound = new SoundPathSpecifier
    ("/Audio/Imperial/Medieval/ES_Bow_Pull_String_3_-_SFX_Producer.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? FullChargeSound = new SoundPathSpecifier
    ("/Audio/Items/bow_pull.ogg");

    [DataField, AutoNetworkedField]
    public float MinChargeTime = 1f;
    [DataField, AutoNetworkedField]
    public float MaxChargeTime = 3f;
    [DataField, AutoNetworkedField]
    public float MinChargeMultiplier = 0.25f;
    [DataField, AutoNetworkedField]
    public float MaxChargeMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float BaseBowDamage = 0f;
    [DataField, AutoNetworkedField]
    public float BaseBluntImpact = 0f;
    [DataField, AutoNetworkedField]
    public float BasePoiseDamage = 0f;

    /// <summary>How long a successful shot delays the shooter's poise recovery.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan PoiseRecoveryDelayAfterAttack = TimeSpan.FromSeconds(5);

    // server-side Damage examine remembers the most recently loaded arrow.
    // Default to the iron arrow until an arrow has been loaded.
    [DataField]
    public EntProtoId LastLoadedArrow = "MedievalNormalArrow";

    [DataField, AutoNetworkedField]
    public float StaminaDrainPerSecond = 5f;
    [DataField, AutoNetworkedField]
    public float StaminaDrainInterval = 0.25f;

    [DataField, AutoNetworkedField]
    public float ChargingMoveSpeedMultiplier = 0.7f;
    [DataField, AutoNetworkedField]
    public EntProtoId ChargeEffectProtoId = "AttackEffect3s";
    [ViewVariables, AutoNetworkedField]
    public EntityUid EffectSpawnedEntity = EntityUid.Invalid;

    [ViewVariables, AutoNetworkedField]
    public bool IsCharging;
    [ViewVariables, AutoNetworkedField]
    public EntityUid? ChargingUser;
    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan ChargeStartTime;

    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan NextStaminaDrainTime;

    [ViewVariables]
    public bool AllowShot;

    [DataField, AutoNetworkedField]
    public float MeleeKnockback = 1.5f;
}

[Serializable, NetSerializable]
public sealed class MedievalBowChargeStart : EntityEventArgs
{
    public readonly NetEntity Bow;

    public MedievalBowChargeStart(NetEntity bow)
    {
        Bow = bow;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalBowChargeCancel : EntityEventArgs
{
    public readonly NetEntity Bow;

    public MedievalBowChargeCancel(NetEntity bow)
    {
        Bow = bow;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalBowChargeRelease : EntityEventArgs
{
    public readonly NetEntity Bow;
    public readonly NetCoordinates Coordinates;
    public readonly GameTick ProjectileInputTick;

    public MedievalBowChargeRelease(
        NetEntity bow,
        NetCoordinates coordinates, GameTick projectileInputTick = default)
    {
        Bow = bow;
        Coordinates = coordinates;
        ProjectileInputTick = projectileInputTick;
    }
}
