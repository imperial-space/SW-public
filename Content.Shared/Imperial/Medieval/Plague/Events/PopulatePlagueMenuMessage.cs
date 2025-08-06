using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class PopulatePlagueMenuMessage : EntityEventArgs
{
    public readonly Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> Data;

    public PopulatePlagueMenuMessage(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data)
    {
        Data = data;
    }
}
