using Robust.Shared.Prototypes;

namespace Content.Shared.ShiftFrontResearch;

[Prototype]
public sealed class ShiftFrontUnitCardPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId UnitName = "";

    [DataField]
    public LocId UnitDesc = "";

    [DataField(required: true)]
    public EntProtoId LinkedProto;

    [DataField(required: true)]
    public int Polymer;

    [DataField(required: true)]
    public int BioShlak;

    [DataField(required: true)]
    public int NanoCarbon;

}
