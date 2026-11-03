using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Dragons.TailSweep;

/// <summary>
/// Dragon tail sweep: the dragon spins in place and knocks down everyone around it.
/// A player dragon uses the action; an AI dragon uses it on its own when enemies get right next to it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class MedievalTailSweepComponent : Component
{
    /// <summary>
    /// Action given to the dragon.
    /// </summary>
    [DataField]
    public EntProtoId Action = "MedievalActionDragonTailSweep";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Sweep radius in tiles, from the dragon's centre.
    /// </summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>
    /// How many tiles hit mobs are thrown back.
    /// </summary>
    [DataField]
    public float ThrowDistance = 2.5f;

    [DataField]
    public float ThrowSpeed = 10f;

    /// <summary>
    /// How long hit mobs stay paralyzed.
    /// </summary>
    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Damage dealt to mobs.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Damage dealt to anchored structures around (walls, doors, tables). Empty means structures are left alone.
    /// Separate from <see cref="Damage"/> because mobs don't take structural damage.
    /// </summary>
    [DataField]
    public DamageSpecifier StructureDamage = new();

    /// <summary>
    /// Structures the sweep must not damage (e.g. game mode objectives). Empty means all are hit.
    /// </summary>
    [DataField]
    public EntityWhitelist? StructureBlacklist;

    /// <summary>
    /// Whether to hit mobs friendly to the dragon by faction (other dragons, hatchlings).
    /// </summary>
    [DataField]
    public bool HitFriendly;

    /// <summary>
    /// Wind-up: how long the dragon spins before the hit. Gives players time to step away.
    /// </summary>
    [DataField]
    public TimeSpan Windup = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Number of steps in one full turn. With a 4-directional sprite that's 4 steps of 90 degrees.
    /// </summary>
    [DataField]
    public int SpinSteps = 4;

    [DataField]
    public SoundSpecifier? WindupSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");

    /// <summary>
    /// Effect spawned on hit (dust ring around the dragon). Empty means no effect.
    /// </summary>
    [DataField]
    public EntProtoId? Effect = "MedievalEffectDragonTailSweep";

    /// <summary>
    /// Effect spawned when the wind-up starts: the trail of the tail while the dragon spins.
    /// It has a 4-directional state and gets the dragon's starting rotation, so it starts behind the dragon.
    /// Empty means no effect.
    /// </summary>
    [DataField]
    public EntProtoId? WindupEffect;

    /// <summary>
    /// AI: sweep if at least this many enemies are within <see cref="AutoRange"/>.
    /// </summary>
    [DataField]
    public int AutoMinTargets = 1;

    /// <summary>
    /// AI: how close enemies must get. Smaller than <see cref="Range"/> so the AI reacts to those standing under the dragon.
    /// </summary>
    [DataField]
    public float AutoRange = 1.8f;

    [DataField]
    public TimeSpan AutoCheckInterval = TimeSpan.FromSeconds(1);

    [AutoPausedField]
    public TimeSpan NextAutoCheck;

    /// <summary>
    /// When the wind-up ends. While not null the dragon can't move.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan? SweepEnd;

    [AutoPausedField]
    public TimeSpan NextSpinStep;

    public int SpinStepsDone;

    public Angle StartRotation;
}

public sealed partial class MedievalTailSweepActionEvent : InstantActionEvent;
