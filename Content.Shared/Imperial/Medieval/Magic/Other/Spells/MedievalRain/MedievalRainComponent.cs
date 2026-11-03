using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalRain;


/// <summary>
/// Spreads <see cref="Count" /> drops over a circle around itself, each one appears at its own moment of <see cref="Duration" />
/// </summary>
[RegisterComponent]
public sealed partial class MedievalRainComponent : Component
{
    /// <summary>
    /// What should fall
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Drop;

    /// <summary>
    /// Effect shown once at the start so everyone can see the area
    /// </summary>
    [DataField]
    public EntProtoId? Marker;

    /// <summary>
    /// How many drops fall in total
    /// </summary>
    [DataField]
    public int Count = 5;

    /// <summary>
    /// Radius of the circle
    /// </summary>
    [DataField]
    public float Radius = 1.5f;

    /// <summary>
    /// Time in seconds over which all drops start falling
    /// </summary>
    [DataField]
    public float Duration = 1.2f;

    [ViewVariables]
    public EntityUid Performer;

    [ViewVariables]
    public List<(TimeSpan At, Vector2 Offset)> Pending = new();
}
