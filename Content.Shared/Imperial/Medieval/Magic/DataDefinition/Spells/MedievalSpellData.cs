using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Magic;


[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class MedievalSpellData
{
    [DataField]
    public GameTick InputTick;

    [DataField]
    public NetEntity Performer;

    [DataField]
    public NetEntity Action;

    [DataField]
    public float CastSpeedModifier;
}
