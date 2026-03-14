using Robust.Shared;
using Robust.Shared.Configuration;
namespace Content.Shared.Imperial.Medieval.Administration.Ships;

/// <summary>
/// Цвары для корабликов, тут лежит ветер максимальная скорость кораблей и подобное
/// </summary>
[CVarDefs]
public sealed class ShipsCCVars : CVars
{
    // максимальная скорость
    public static readonly CVarDef<float> ShipsMaxSpeed =
        CVarDef.Create("ships.maxspeed", 20f, CVar.REPLICATED | CVar.SERVER);
    // как часто меняется ветер
    public static readonly CVarDef<float> WindChangeTime =
        CVarDef.Create("ships.windchangetime", 1f, CVar.REPLICATED | CVar.SERVER);
    // как часто ветер дует
    public static readonly CVarDef<float> WindDelay =
        CVarDef.Create("ships.winddelay", 1f, CVar.REPLICATED | CVar.SERVER);
    // как часто появляются волны
    public static readonly CVarDef<float> WaveDelay =
        CVarDef.Create("ships.wavedelay", 1f, CVar.REPLICATED | CVar.SERVER);
    // Минимальный уровень для поломки лодки (Шторма если кто не понял)
    public static readonly CVarDef<bool> WindEnabled =
        CVarDef.Create("ships.waveenabled", true, CVar.REPLICATED | CVar.SERVER);
    // сила с которой ветер толкает
    public static readonly CVarDef<float> WindPower =
        CVarDef.Create("ships.windpower", 1f, CVar.REPLICATED | CVar.SERVER);
    // угол поворота ветра
    public static readonly CVarDef<float> WindRotation =
        CVarDef.Create("ships.windrotation", 0f, CVar.REPLICATED | CVar.SERVER);
    // уровень шторма
    public static readonly CVarDef<float> StormLevel =
        CVarDef.Create("ships.stormlevel", 1f, CVar.REPLICATED | CVar.SERVER);
    // скорость с которой волна спавнится
    public static readonly CVarDef<float> WaveForce =
        CVarDef.Create("ships.waveforce", 1f, CVar.REPLICATED | CVar.SERVER);
    // радиус спавна волн
    public static readonly CVarDef<float> WaveSpawnRange =
        CVarDef.Create("ships.wavespawnrange", 40f, CVar.REPLICATED | CVar.SERVER);
    // угол разброса волн
    public static readonly CVarDef<float> WaveSpawnAngle =
        CVarDef.Create("ships.wavespawnangle", 10f, CVar.REPLICATED | CVar.SERVER);
    // в каком радиусе ломает волна
    public static readonly CVarDef<float> WaveRadiusTiles =
        CVarDef.Create("ships.waveradiustiles", 3f, CVar.REPLICATED | CVar.SERVER);
    // какое максимальное количество тайлов может сломать волна
    public static readonly CVarDef<int> WaveMaxBreakCount =
        CVarDef.Create("ships.wavemaxbreakcount", 3, CVar.REPLICATED | CVar.SERVER);
    // Минимальный уровень для поломки лодки (Шторма если кто не понял)
    public static readonly CVarDef<int> WaveMinToBreakLevel =
        CVarDef.Create("ships.wavemintobreaklevel", 2, CVar.REPLICATED | CVar.SERVER);

}

