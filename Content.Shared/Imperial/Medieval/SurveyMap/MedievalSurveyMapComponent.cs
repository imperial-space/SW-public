using System.Numerics;

namespace Content.Shared.Imperial.Medieval.SurveyMap;


[RegisterComponent]
public sealed partial class MedievalSurveyMapComponent : Component
{
    [DataField]
    public EntityUid? SurveyedMap;

    [DataField]
    public Vector2 SurveyCenter;

    [DataField]
    public float SurveyRange = 256f;

    [DataField]
    public LocId OpenMapText = "medieval-open-map";

    [DataField]
    public Vector2 Size = new Vector2(790, 790);

    /// <summary>Field notes belong to this physical map and travel with it.</summary>
    [DataField]
    public List<MedievalSurveyMapAnnotation> Annotations = new();
}
