using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SurveyMap;

[Serializable, NetSerializable]
public enum MedievalSurveyMapUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MedievalSurveyMapState : BoundUserInterfaceState
{
    public Vector2 Size = new(790, 790);
    public MedievalSurveyMapGeography? Geography;
    public int? DisplayedWorldMap;
    public List<MedievalSurveyMapAnnotation> Annotations = new();
    public NetEntity? Surveyor;
    public int PendingAnnotationIndex = -1;
    public string PendingDescription = "";
}

/// <summary>Fixed regional extent, without player tracking or radar contacts.</summary>
[Serializable, NetSerializable]
public sealed class MedievalSurveyMapGeography(NetCoordinates coordinates, float range)
{
    public NetCoordinates Coordinates = coordinates;
    public float Range = range;
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class MedievalSurveyMapAnnotation
{
    [DataField] public Vector2 WorldPosition;
    [DataField] public int WorldMap;
    [DataField] public string Title = "";
    [DataField] public string Description = "";
}

[Serializable, NetSerializable]
public sealed class MedievalSurveyMapAnnotateMessage(string title) : BoundUserInterfaceMessage
{
    public string Title = title;
}
