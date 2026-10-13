namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Shared level conventions; individual modules define their own gameplay coefficients.</summary>
public static class SkillScaling
{
    public const int Baseline = 10;
    public const int Basic = 4;
    public const int Trained = 8;
    public const int Expert = 12;
    public const int Master = 16;
    public const int Legendary = 20;

    public static float Multiplier(int level, float perLevel) =>
        Math.Max(0.05f, 1f + (Math.Clamp(level, 1, Legendary) - Baseline) * perLevel);

    public static int Level(SkillsComponent skills, string id) => skills.Levels.GetValueOrDefault(id, Baseline);
}
