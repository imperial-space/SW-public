namespace Content.Shared.Imperial.Medieval.Skills;

/// <summary>Pure progression rules shared by prediction, server authority and descriptions.</summary>
public static class SkillScaling
{
    public const int Baseline = 10;
    public const int Basic = 4;
    public const int Trained = 8;
    public const int Expert = 12;
    public const int Master = 16;
    public const int Legendary = 20;

    public static float Multiplier(int level, float perLevel) =>
        Math.Max(0.05f, 1f + (Math.Clamp(level, 1, 20) - Baseline) * perLevel);

    public static float HealingReceived(int level, float low = 0.05f, float high = 0.015f) =>
        Multiplier(level, level < Baseline ? low : high);

    public static int Level(SkillsComponent skills, string id) => skills.Levels.GetValueOrDefault(id, Baseline);

    public static int JumpCharges(int level) => level >= Legendary ? 3 : level >= Master ? 2 : 1;

    public static int PointCost(int level)
    {
        level = Math.Clamp(level, 1, 20);
        if (level <= Baseline)
            return Baseline - level + (level <= Basic ? 1 : 0) + (level == 1 ? 1 : 0);
        var cost = 0;
        for (var i = Baseline + 1; i <= level; i++)
            cost += i <= Expert ? 1 : i <= Master ? 2 : i < Legendary ? 3 : 4;
        return -cost;
    }

    public static float StealSuccess(float baseChance, float modifier, int victimIntelligence) =>
        Math.Clamp(baseChance * modifier - (victimIntelligence - Baseline) * 0.01f, 0.05f, 0.95f);
}
