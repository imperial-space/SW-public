using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalBloodMark;


/// <summary>
/// The entity takes extra damage: <see cref="Multiplier" /> of every damage it receives is added on top
/// </summary>
[RegisterComponent]
public sealed partial class MedievalBloodMarkComponent : Component
{
    /// <summary>
    /// 0.25 means +25% of the received damage
    /// </summary>
    [ViewVariables]
    public float Multiplier;

    [ViewVariables]
    public TimeSpan ExpireAt;

    /// <summary>
    /// Whole lifetime of the mark, the sign loses its parts while the time runs out
    /// </summary>
    [ViewVariables]
    public TimeSpan Total;

    /// <summary>
    /// The stamp animation is playing until this time
    /// </summary>
    [ViewVariables]
    public TimeSpan StampUntil;

    [ViewVariables]
    public MedievalBloodMarkStage Visual;

    [ViewVariables]
    public EntityUid? Effect;

    /// <summary>
    /// Extra damage is being dealt right now, it must not be marked again
    /// </summary>
    [ViewVariables]
    public bool Applying;
}


/// <summary>
/// Appearance key of the mark sign, the value is the stage number
/// </summary>
[Serializable, NetSerializable]
public enum MedievalBloodMarkVisuals : byte
{
    Stage,
}

/// <summary>
/// Value of <see cref="MedievalBloodMarkVisuals.Stage" />: the stamp animation and then the sign losing its parts
/// </summary>
[Serializable, NetSerializable]
public enum MedievalBloodMarkStage : byte
{
    Appear,
    Stage0,
    Stage1,
    Stage2,
    Stage3,
    Stage4,
}
