using System.Linq;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

[RegisterComponent]
public sealed partial class UpgradeArmorOnSmithCompleteComponent : Component
{
    [DataField]
    public Dictionary<int, SmithQualityModifiers> ItemQualityTable { get; set; } = new()
    {
        {-2, new SmithQualityModifiers(ItemQuality.Bad, 1.0f)},
        {2, new SmithQualityModifiers(ItemQuality.Default, 1.05f)},
        {12, new SmithQualityModifiers(ItemQuality.Good, 1.1f)},
        {18, new SmithQualityModifiers(ItemQuality.Excellent, 1.2f)}
    };

    public SmithQualityModifiers GetBestModifier(int score)
    {
        var bestData = ItemQualityTable.MinBy(x => x.Key).Value; // Самый плохой по умолчанию

        foreach (var (threshold, data) in ItemQualityTable)
        {
            if (score > threshold && data.Modifier > bestData.Modifier)
            {
                bestData = data;
            }
        }

        return bestData;
    }
}

[Serializable]
public enum ItemQuality
{
    Bad,
    Default,
    Good,
    Excellent
}

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


[RegisterComponent]
public sealed partial class UpgradeWeaponOnSmithCompleteComponent : Component
{
    [DataField]
    public Dictionary<int, SmithQualityModifiers> ItemQualityTable { get; set; } = new()
    {
        {-2, new SmithQualityModifiers(ItemQuality.Bad, 1.0f)},
        {2, new SmithQualityModifiers(ItemQuality.Default, 1.05f)},
        {12, new SmithQualityModifiers(ItemQuality.Good, 1.1f)},
        {18, new SmithQualityModifiers(ItemQuality.Excellent, 1.2f)}
    };

    public SmithQualityModifiers GetBestModifier(int score)
    {
        var bestData = ItemQualityTable.MinBy(x => x.Key).Value; // Самый плохой по умолчанию

        foreach (var (threshold, data) in ItemQualityTable)
        {
            if (score > threshold && data.Modifier > bestData.Modifier)
            {
                bestData = data;
            }
        }

        return bestData;
    }

}

[RegisterComponent]
public sealed partial class DeleteOnLowScoreOnSmithCompleteComponent : Component
{
    [DataField(required: true)]
    public int Threshold;
}
