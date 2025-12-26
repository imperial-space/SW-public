namespace Content.Shared.Imperial.Medieval.Magic.ManaBurn;

/// <summary>
/// A component that will reduce the mana of an entity within range.
/// Mana will decrease by BurnQuantity every BurnDelay seconds while the entity is in range.
/// </summary>
[RegisterComponent]
public sealed partial class ManaBurnFieldComponent : Component
{
    /// <summary>
    /// Reducing delay
    /// </summary>
    [DataField(required: true)]
    public TimeSpan BurnDelay;
    /// <summary>
    /// The amount of mana being reduced
    /// </summary>
    [DataField]
    public float BurnQuantity = 5;
    /// <summary>
    /// Range of action
    /// </summary>
    [DataField]
    public float Radius = 3f;
    /// <summary>
    /// The popup that will be sent to the entity after each trigger
    /// </summary>
    [DataField]
    public LocId BurnPopup = string.Empty;
    [ViewVariables]
    public TimeSpan BurnTime = TimeSpan.Zero;
}
