using Robust.Shared;
using Robust.Shared.Configuration;
namespace Content.Shared.Imperial.Medieval.Administration.Ships;

/// <summary>
/// Цвары для корабликов, тут лежит ветер максимальная скорость кораблей и подобное
/// </summary>
[CVarDefs]
public sealed class ShipsCCVars : CVars
{
    public static readonly CVarDef<float> ShipsMaxSpeed =
        CVarDef.Create("ships.maxspeed", 20f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WindChangeTime =
        CVarDef.Create("ships.windchangetime", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WindDelay =
        CVarDef.Create("ships.winddelay", 1f, CVar.REPLICATED | CVar.SERVER);
    public static readonly CVarDef<float> WaveDelay =
        CVarDef.Create("ships.wavedelay", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WindPower =
        CVarDef.Create("ships.windpower", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WindRotation =
        CVarDef.Create("ships.windrotation", 0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> StormLevel =
        CVarDef.Create("ships.stormlevel", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WaveForce =
        CVarDef.Create("ships.waveforce", 1f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> WaveSpawnRange =
        CVarDef.Create("ships.wavespawnrange", 40f, CVar.REPLICATED | CVar.SERVER);
}

