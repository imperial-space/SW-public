using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.MedievalSpellSleep;


/// <summary>
///
/// </summary>
[RegisterComponent]
public sealed partial class MedievalSleepTargetComponent : Component
{
    /// <summary>
    /// How much damage of any type it takes to wake this entity.
    /// </summary>
    [DataField]
    public FixedPoint2 WakeThreshold = FixedPoint2.New(50);

    /// <summary>
    ///     Cooldown time between users hand interaction.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10f);

    [DataField]
    public EntProtoId? SpawnedEffect;

    [DataField]
    public bool CanPutToSleep = true;

    [DataField]
    public EntityUid? Caster;
}
