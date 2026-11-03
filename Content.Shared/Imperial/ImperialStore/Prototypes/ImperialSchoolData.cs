using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ImperialStore;

/// <summary>
/// imperial medieval - everything a school of magic needs around an essence slot of a grimoire, in one place.
/// A currency with this set is a school: it can be put into a slot, see <see cref="ImperialStoreSlots"/>.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImperialSchoolData
{
    /// <summary>
    /// The place of the school in the list of the window.
    /// </summary>
    [DataField(required: true)]
    public int Order;

    /// <summary>
    /// The name of the school in the window.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The word the player says while the school is put into a slot.
    /// </summary>
    [DataField(required: true)]
    public LocId Word;

    /// <summary>
    /// The colour of the word, the effect around the player has the same one.
    /// </summary>
    [DataField(required: true)]
    public Color Color;

    /// <summary>
    /// The effect that lasts around the player while the school is put into a slot.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Effect;

    /// <summary>
    /// The sign of the school, a texture of the grimoire window.
    /// </summary>
    [DataField(required: true)]
    public string Rune = string.Empty;

    /// <summary>
    /// The colour of the school in the window when the colour of the currency does not read on the page.
    /// </summary>
    [DataField]
    public Color? WindowColor;

    /// <summary>
    /// The cloth of the ribbon of the school when its colour turns ugly when it is darkened.
    /// </summary>
    [DataField]
    public Color? ClothColor;
}
