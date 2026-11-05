using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Medieval.Poise.MeleeLockOn;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class MeleeLockOnComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public bool TargetingMode;

    // Input ownership only; ranged spells keep their own targets and casting state.
    [ViewVariables, AutoNetworkedField]
    public bool RangedTargetingActive;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Normal range as a fraction of the Player-centered viewport. Race mob
    /// prototypes can override it. A visible Target within this range restores time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FirstDistanceThreshold = 1f;

    [DataField, AutoNetworkedField]
    public float CameraPanStartThreshold = 0.5f;

    /// <summary>Extra range for Player-relative camera follow during the lock timeout.</summary>
    [DataField, AutoNetworkedField]
    public float CameraFollowRangeExtension = 0.3f;

    /// <summary>Pan 10 percent farther toward the target than the base follow offset.</summary>
    [DataField, AutoNetworkedField]
    public float CameraPanMultiplier = 1.1f;

    /// <summary>Maximum pan as a fraction of the viewport half-size, keeping the Player inside this screen margin.</summary>
    [DataField, AutoNetworkedField]
    public float MaxCameraPanFraction = 0.75f;

    [DataField, AutoNetworkedField]
    public TimeSpan LockOnTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Continuous valid-lock time required before the Player's timer refills.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LockOnRecoveryDelay = TimeSpan.FromSeconds(1);

    /// <summary>Lock-on seconds restored per second of a sustained visible, in-range lock.</summary>
    [DataField, AutoNetworkedField]
    public float LockOnRecoveryRate = 1f;

    /// <summary>Player-owned remaining time. Direct Target switches preserve it; actual unlocks refill it.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan LockOnTimeRemaining = TimeSpan.FromSeconds(15);

    // Absolute clocks are shifted together by MeleeLockOnSystem on unpause;
    // the networked deadline also needs its field-delta marker updated.
    [ViewVariables]
    public TimeSpan? LockOnTimerLastUpdated;

    [ViewVariables]
    public TimeSpan? LockOnRecoveryStartedAt;

    [ViewVariables]
    public bool IsLockOnTimeDraining;

    [ViewVariables]
    public TimeSpan NextLockOnTimerSync;

    [DataField, AutoNetworkedField]
    public TimeSpan CameraTargetLossTolerance = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan LockOnExpiryWarningDuration = TimeSpan.FromSeconds(5);

    [ViewVariables, AutoNetworkedField]
    public uint LockGeneration;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan? LockOnExpiresAt;
}

public sealed partial class MeleeLockOnToggleEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed class MeleeLockOnTargetEvent(NetEntity? target, bool exitTargeting = false) : EntityEventArgs
{
    public readonly NetEntity? Target = target;
    public readonly bool ExitTargeting = exitTargeting;
}

/// <summary>
/// Only the viewport geometry and screen visibility come from the client. The server
/// bounds the geometry, verifies line of sight, and owns range and expiration.
/// </summary>
[Serializable, NetSerializable]
public sealed class MeleeLockOnViewEvent(NetEntity target, uint generation, Vector2 halfExtents, bool visible) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly uint Generation = generation;
    public readonly Vector2 HalfExtents = halfExtents;
    public readonly bool Visible = visible;
}

[ByRefEvent]
public readonly record struct MeleeLockOnChangedEvent;

/// <summary>Server-only working state, present only while a user has a target.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveMeleeLockOnComponent : Component
{
    public Vector2 ViewHalfExtents = new(10.5f, 7.5f);
    [AutoPausedField]
    public TimeSpan LastReport;
    public bool ReportedVisible;
    public bool HasViewReport;
    public bool Unoccluded;
    [AutoPausedField]
    public TimeSpan NextVisibilityCheck;
}

/// <summary>Tracks current watchers and the Target's private warning cooldown; no per-tick target scans.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MeleeLockOnTrackedTargetComponent : Component
{
    public readonly HashSet<EntityUid> Watchers = new();

    /// <summary>Minimum time between hostile-intent popups, shared across everyone locking this Target.</summary>
    [DataField]
    public TimeSpan HostileIntentCooldown = TimeSpan.FromSeconds(10);

    [ViewVariables, AutoPausedField]
    public TimeSpan NextHostileIntentAt;
}
