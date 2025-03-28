using System.Linq;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

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
}
