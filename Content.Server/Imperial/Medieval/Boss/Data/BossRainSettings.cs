using System.Linq;

namespace Content.Server.Imperial.Medieval.Boss;

[DataDefinition]
public sealed partial class BossRainSettings
{
    [DataField] public int Count = 4;                                            // total crystals in this step
    [DataField] public float WarningTime = 3;                                   // seconds before each crystal hits
    [DataField] public List<int> Sizes = new() { 3 };                            // footprint sizes in tiles, repeated values add weight
    [DataField] public float CastTime = 1.5f;
    [DataField] public int Concurrent = 8;                                      // starting warning limit
    [DataField] public int MaxConcurrent = 8;                                   // warning limit at full pressure
    [DataField] public float SpawnInterval = 0.5f;
    [DataField] public float MinSpawnInterval = 0.5f;
    [DataField] public float DoubleChance;                                      // chance to spawn two crystals together
    [DataField] public float RampSeconds = 60;                                  // time to reach full pressure

    public bool IsValid => Count > 0 && Sizes.Count > 0 && Sizes.All(size => size is 2 or 3) &&
        Time(WarningTime) && Time(CastTime) && Time(SpawnInterval) && Time(MinSpawnInterval) && Time(RampSeconds) &&
        Concurrent > 0 && MaxConcurrent >= Concurrent && float.IsFinite(DoubleChance) && DoubleChance is >= 0 and <= 1;
    private static bool Time(float value) => float.IsFinite(value) && value is > 0 and <= 3600;
}
