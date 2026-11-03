using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalSpawnExtraInHand;


/// <summary>
/// After an in-hand spell spawns its item, spawns one more item into the other hand
/// </summary>
[RegisterComponent]
public sealed partial class MedievalSpawnExtraInHandComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}
