using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

[RegisterComponent]
public sealed partial class IslandRadialGenerationComponent : Component
{
    [DataField]
    public List<ResPath> LowIslands = new();

    [DataField]
    public List<ResPath> MediumIslands = new();

    [DataField]
    public List<ResPath> HighIslands = new();

    [DataField]
    public float LowIslandMinRange = 50f;

    [DataField]
    public float MediumIslandMinRange = 100f;

    [DataField]
    public float HighIslandMinRange = 200f;

    [DataField]
    public float HighIslandMaxRange = 400f;

    [DataField]
    public float InterIslandsThreshold = 10f;

    [DataField]
    public int MaxCandidatesPerPoint = 30;
}
