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
    public float LowIslandMinRange = 150f;

    [DataField]
    public float MediumIslandMinRange = 250f;

    [DataField]
    public float HighIslandMinRange = 350f;

    [DataField]
    public float HighIslandMaxRange = 450f;

    [DataField]
    public float InterIslandsThreshold = 16f;

    [DataField]
    public int MaxCandidatesPerPoint = 30;
}
