using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>One weapon's stamina, preparation, commitment and recovery state.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvancedLightAttackComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<AdvancedLightAttackPresetPrototype> Preset = "Default";

    [DataField, AutoNetworkedField]
    public float? StaminaCost;

    [DataField, AutoNetworkedField]
    public float? Windup;

    [DataField, AutoNetworkedField]
    public float? RaycastArc;

    [DataField, AutoNetworkedField]
    public EntProtoId? WindupEffect;

    // minimum visible heavy wind-up; maximum and cost remain on ChargedAttack.
    [DataField, AutoNetworkedField]
    public float? ChargeWindup;

    [DataField, AutoNetworkedField]
    public EntProtoId? ChargeWindupEffect;

    // Runtime fields deliberately have no DataField: prototype copying must not
    // try to serialize input events, sequence numbers or clock anchors.
    [ViewVariables, AutoNetworkedField]
    public AdvancedLightAttackPhase Phase;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Attacker;

    [ViewVariables, AutoNetworkedField]
    public LightAttackEvent? PendingAttack;

    [ViewVariables, AutoNetworkedField]
    public uint AttackId;

    [ViewVariables, AutoNetworkedField]
    public uint Revision;

    // light and charged attacks share one commitment per weapon.
    [ViewVariables, AutoNetworkedField]
    public bool IsCharged;

    [ViewVariables, AutoNetworkedField]
    public bool SecondaryHeld;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan MinimumStrike;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan MaximumStrike;

    [ViewVariables, AutoNetworkedField]
    public bool PrimaryHeld;

    [ViewVariables, AutoNetworkedField]
    public bool UsesPrimaryLatch;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan Started;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan CueStarts;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan Strikes;

    // Presentation status is separate from gameplay recovery: consuming a
    // commitment does not prove that the normal swing checks succeeded.
    [ViewVariables, AutoNetworkedField]
    public AdvancedMeleeVisualState PresentationState;

    [ViewVariables, AutoNetworkedField]
    public AdvancedMeleeVisualData? Presentation;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan PresentationStrike;

    // A paired real/simulation sample maps observer visuals onto server time.
    // Local attack prediction continues to use simulation time exclusively.
    [ViewVariables, AutoNetworkedField]
    public TimeSpan ServerRealSample;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan ServerSimulationSample;

    // Local input-send bookkeeping, deliberately excluded from network state
    // and rollback. Only the owner's first-predicted input update advances it.
    public AdvancedLightAttackAimThrottle ClientAimThrottle;
}

[Serializable, NetSerializable]
public enum AdvancedLightAttackPhase : byte
{
    Idle,
    Preparing,
    Windup,
    Recovery,
}

/// <summary>Derived wielder status; the two weapons retain independent timelines.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AdvancedLightAttackUserComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool IsDualWielding;

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan AttackLockedUntil;
}
