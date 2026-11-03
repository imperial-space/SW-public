using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalBloodMark;


/// <summary>
/// After an aimed spawn puts <see cref="MedievalBloodMarkComponent" /> on the selected target and deletes itself
/// </summary>
[RegisterComponent]
public sealed partial class MedievalApplyBloodMarkComponent : Component
{
    /// <summary>
    /// Extra damage part: 0.25 means +25%
    /// </summary>
    [DataField]
    public float Multiplier = 0.15f;

    /// <summary>
    /// How long the mark lasts in seconds
    /// </summary>
    [DataField]
    public float Duration = 10f;

    /// <summary>
    /// Visual effect attached to the marked entity
    /// </summary>
    [DataField]
    public EntProtoId? Effect;
}
