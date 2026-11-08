using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.BadSmell;

/// <summary>
/// Lets a character wash up at this entity while it holds enough of the reagent. The reagent is not spent.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalWashSourceComponent : Component
{
    [DataField]
    public string Solution = "tank";

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Water";

    [DataField]
    public float MinAmount = 60f;

    /// <summary>
    /// The reagent must make up at least this share of the whole solution.
    /// </summary>
    [DataField]
    public float MinFraction = 0.8f;

    [DataField]
    public float Duration = 5f;

    /// <summary>
    /// How much smell one wash removes.
    /// </summary>
    [DataField]
    public float SmellReduction = 20f;

    /// <summary>
    /// Washing never takes the smell level below this value.
    /// </summary>
    [DataField]
    public float SmellFloor = 40f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg");

    /// <summary>
    /// The sound repeats with this interval while the character is washing.
    /// </summary>
    [DataField]
    public float SoundInterval = 1.7f;
}

/// <summary>
/// Marks a character that is washing right now, so the splash sound repeats until the do-after ends.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalWashingComponent : Component
{
    public EntityUid Source;

    public TimeSpan NextSound;
}

[Serializable, NetSerializable]
public sealed partial class MedievalWashDoAfterEvent : SimpleDoAfterEvent;
