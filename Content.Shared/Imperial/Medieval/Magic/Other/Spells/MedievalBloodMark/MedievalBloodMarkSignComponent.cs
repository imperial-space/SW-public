namespace Content.Shared.Imperial.Medieval.Magic.MedievalBloodMark;


/// <summary>
/// The client puts the sign above the top of the sprite of the entity it is attached to,
/// so the sign is not inside big creatures
/// </summary>
[RegisterComponent]
public sealed partial class MedievalBloodMarkSignComponent : Component
{
    /// <summary>
    /// Distance in tiles between the top of the marked sprite and the center of the sign
    /// </summary>
    [DataField]
    public float Margin = 0.35f;

    /// <summary>
    /// Offset that is already applied to the sprite, the client sets it again only when it changes
    /// </summary>
    [ViewVariables]
    public float? AppliedOffset;
}
