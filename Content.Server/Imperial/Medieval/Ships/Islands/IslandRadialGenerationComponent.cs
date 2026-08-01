using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

[RegisterComponent]
public sealed partial class IslandRadialGenerationComponent : Component
{
    [DataField]
    public List<ResPath> LowIslands = new()
    {
        new("/Maps/Imperial/Medieval/Islands/IslandLow1.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow2.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow3.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow4.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow13.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow14.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow15.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow16.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow25.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow26.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow27.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow28.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow38.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow39.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow40.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow41.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow50.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow51.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow52.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandLow53.yml"),
    };

    [DataField]
    public List<ResPath> MediumIslands = new()
    {
        new("/Maps/Imperial/Medieval/Islands/IslandMedium5.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium6.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium7.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium8.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium17.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium18.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium19.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium20.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium29.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium30.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium31.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium32.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium33.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium42.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium43.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium44.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium45.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium54.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium55.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium56.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandMedium57.yml"),
    };

    [DataField]
    public List<ResPath> HighIslands = new()
    {
        new("/Maps/Imperial/Medieval/Islands/IslandHard9.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard10.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard11.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard12.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard21.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard22.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard23.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard24.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard34.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard35.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard36.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard37.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard46.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard47.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard48.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard49.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard58.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard59.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard60.yml"),
        new("/Maps/Imperial/Medieval/Islands/IslandHard61.yml"),
    };

    [DataField]
    public int LowIslandCount = 6;

    [DataField]
    public int MediumIslandCount = 6;

    [DataField]
    public int HighIslandCount = 6;

    [DataField]
    public float LowIslandMinRange = 530f;

    [DataField]
    public float MediumIslandMinRange = 100f;

    [DataField]
    public float HighIslandMinRange = 1400f;

    [DataField]
    public float HighIslandMaxRange = 2000f;

    [DataField]
    public float InterIslandsThreshold = 16f;

    [DataField]
    public int MaxCandidatesPerPoint = 30;
}
