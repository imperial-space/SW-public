using Robust.Shared;
using Robust.Shared.Configuration;
namespace Content.Shared.Imperial.Medieval.Administration.Ships;

/// <summary>
/// Цвары для корабликов, тут лежит ветер максимальная скорость кораблей и подобное
/// </summary>
[CVarDefs]
public sealed class ShipsCCVars : CVars
{
    /// <summary>
    /// максимальная скорость
    /// </summary>
    public static readonly CVarDef<float> ShipsMaxSpeed =
        CVarDef.Create("ships.maxspeed", 20f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// как часто меняется ветер
    /// </summary>
    public static readonly CVarDef<float> WindChangeTime =
        CVarDef.Create("ships.windchangetime", 10f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// как часто ветер дует
    /// </summary>
    public static readonly CVarDef<float> WindDelay =
        CVarDef.Create("ships.winddelay", 1f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// Частота спавна волн
    /// </summary>
    public static readonly CVarDef<float> WaveDelay =
        CVarDef.Create("ships.wavedelay", 1f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// Работает ли ветер
    /// </summary>
    public static readonly CVarDef<bool> WindEnabled =
        CVarDef.Create("ships.windenabled", true, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// сила с которой ветер толкает
    /// </summary>
    public static readonly CVarDef<float> WindPower =
        CVarDef.Create("ships.windpower", 1f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// угол поворота ветра
    /// </summary>
    public static readonly CVarDef<float> WindRotation =
        CVarDef.Create("ships.windrotation", 0f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// уровень шторма
    /// </summary>
    public static readonly CVarDef<float> StormLevel =
        CVarDef.Create("ships.stormlevel", 1f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormChangeTime =
        CVarDef.Create("ships.stormchangetime", 180f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormIncreaseChance =
        CVarDef.Create("ships.stormincreasechance", 0.10f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormDecreaseChance =
        CVarDef.Create("ships.stormdecreasechance", 0.15f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormMinLevel =
        CVarDef.Create("ships.stormminlevel", 1f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormMaxLevel =
        CVarDef.Create("ships.stormmaxlevel", 3f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> StormRainLevel =
        CVarDef.Create("ships.stormrainlevel", 3f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<string> StormRainWeather =
        CVarDef.Create("ships.stormrainweather", "Rain", CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// скорость с которой волна спавнится
    /// </summary>
    public static readonly CVarDef<float> WaveForce =
        CVarDef.Create("ships.waveforce", 1f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// радиус спавна волн
    /// </summary>
    public static readonly CVarDef<float> WaveSpawnRange =
        CVarDef.Create("ships.wavespawnrange", 40f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// угол разброса волн
    /// </summary>
    public static readonly CVarDef<float> WaveSpawnAngle =
        CVarDef.Create("ships.wavespawnangle", 10f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// в каком радиусе ломает волна
    /// </summary>
    public static readonly CVarDef<float> WaveRadiusTiles =
        CVarDef.Create("ships.waveradiustiles", 3f, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// какое максимальное количество тайлов может сломать волна
    /// </summary>
    public static readonly CVarDef<int> WaveMaxBreakCount =
        CVarDef.Create("ships.wavemaxbreakcount", 3, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// Минимальный уровень для поломки лодки (Шторма если кто не понял)
    /// </summary>
    public static readonly CVarDef<int> WaveMinToBreakLevel =
        CVarDef.Create("ships.wavemintobreaklevel", 2, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// размер карты
    /// </summary>
    public static readonly CVarDef<int> MapScale =
        CVarDef.Create("ships.mapscale", 100, CVar.REPLICATED | CVar.SERVER);
    /// <summary>
    /// радиус с которого телепортирует корабль
    /// </summary>
    public static readonly CVarDef<int> TeleportRange =
        CVarDef.Create("ships.teleportrange", 50, CVar.REPLICATED | CVar.SERVER);

}

