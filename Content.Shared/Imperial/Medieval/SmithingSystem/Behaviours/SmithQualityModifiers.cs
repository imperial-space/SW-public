namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

[Serializable, DataDefinition]
public sealed partial class SmithQualityModifiers
{
    [DataField]
    public ItemQuality Quality { get; set; }

    [DataField]
    public float Modifier { get; set; }

    private SmithQualityModifiers()
    {

    }

    public SmithQualityModifiers(ItemQuality quality, float modifier)
    {
        Quality = quality;
        Modifier = modifier;
    }
}
