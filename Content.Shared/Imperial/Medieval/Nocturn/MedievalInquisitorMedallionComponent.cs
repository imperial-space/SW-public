using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Nocturn.Components;

[RegisterComponent]
public sealed partial class MedievalInquisitorMedallionComponent : Component
{
    [DataField]
    public TimeSpan ExaminationDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public ProtoId<SpeciesPrototype> HumanSpecies = "Human";

    [DataField]
    public DamageSpecifier AncientNocturneDamage = new()
    {
        DamageDict =
        {
            { "Heat", 5000 }
        }
    };
}

[RegisterComponent]
public sealed partial class NocturnHumanBloodProhibitionComponent : Component;

[Serializable, NetSerializable]
public enum InquisitorMedallionTargetKind : byte
{
    Human,
    YoungNocturne,
    AncientNocturne
}

[Serializable, NetSerializable]
public sealed partial class MedievalInquisitorMedallionDoAfterEvent : DoAfterEvent
{
    [DataField]
    public InquisitorMedallionTargetKind TargetKind { get; private set; }

    private MedievalInquisitorMedallionDoAfterEvent()
    {
    }

    public MedievalInquisitorMedallionDoAfterEvent(InquisitorMedallionTargetKind targetKind)
    {
        TargetKind = targetKind;
    }

    public override DoAfterEvent Clone()
    {
        return new MedievalInquisitorMedallionDoAfterEvent(TargetKind);
    }
}
