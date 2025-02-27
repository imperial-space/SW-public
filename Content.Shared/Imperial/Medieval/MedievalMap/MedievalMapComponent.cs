using System.Numerics;

namespace Content.Shared.Imperial.Medieval.MedievalMap;


[RegisterComponent]
public sealed partial class MedievalMapComponent : Component
{
    [DataField(required: true)]
    public string MapTexturePath = "";

    [DataField]
    public LocId OpenMapText = "medieval-open-map";

    [DataField]
    public Vector2 Size = new Vector2(790, 790);
}
