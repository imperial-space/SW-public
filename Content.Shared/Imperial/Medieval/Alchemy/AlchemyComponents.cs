using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyIngredientComponent : Component
{
    [DataField(required: true)] public string Profile = default!;
}

[RegisterComponent]
public sealed partial class AlchemyVesselComponent : Component
{
    [DataField] public string Solution = "beaker";
    [DataField] public float NigredoTemperature = 350;
    [DataField] public ProtoId<AlchemyOperationPrototype> HeatOperation = "Heat";
    [DataField] public ProtoId<AlchemyOperationPrototype> CoolOperation = "Cool";
    [DataField] public SoundSpecifier StepSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");
    [DataField] public bool Processing;
    [DataField] public bool Hot;
    [DataField] public bool Cold;
}

[RegisterComponent]
public sealed partial class AlchemyApparatusComponent : Component
{
    [DataField] public string Operation = "Distill";
    [DataField] public string OutputContainer = "alchemy_output";
    [DataField] public int OutputCapacity = 10;
    [DataField] public bool OutputToInput;
    [DataField] public string Solution = "alchemy_input";
    public bool IsProcessing;
    public uint ProcessingGeneration;
    public EntityUid? Input;
    public FixedPoint2 InputVolume;
    public EntityUid? User;
    public bool Completing;
    public List<EntityUid> Items = new();
}

[RegisterComponent]
public sealed partial class AlchemyItemHistoryComponent : Component
{
    [DataField] public List<string> Operations = new();
}
