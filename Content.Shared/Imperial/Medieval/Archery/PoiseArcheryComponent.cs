using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Archery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoiseArcheryComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype> DamageType = "Piercing";

    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 1f;

    // optional second type with independent charged-bow scaling.
    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype>? SecondaryDamageType;

    [DataField, AutoNetworkedField]
    public float SecondaryFixedDamage = 0f;

    [DataField, AutoNetworkedField]
    public float SecondaryDamageMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float BluntMultiplier = 0.25f;

    [DataField, AutoNetworkedField]
    public float MinPoiseMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float MaxPoiseMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float FixedDamage = 10f;

    [ViewVariables]
    public int FiredPoiseDamage;

    [ViewVariables]
    public bool PoiseHitApplied;
}

[RegisterComponent]
public sealed partial class PayloadArrowComponent : Component
{
    [DataField]
    public string PayloadSlot = "payload";
    [DataField]
    public string SolutionName = "drink";
    [ViewVariables]
    public bool Spent;

    [DataField]
    public HashSet<string> AllowedReagentGroups = new()
{
    "Toxins"
};
    [DataField]
    public HashSet<ProtoId<ReagentPrototype>> AllowedReagents = new();
}

[RegisterComponent]
public sealed partial class DualChamberArrowComponent : Component
{
    [DataField]
    public string ChamberOneSlot = "chamber1";
    [DataField]
    public string ChamberTwoSlot = "chamber2";

    [DataField]
    public string SolutionName = "drink";
    [DataField]
    public string ReactionSolutionName = "reaction";

    [DataField]
    public int ActiveChamber = 1;

    [DataField]
    public SoundSpecifier BreakSound = new SoundCollectionSpecifier("GlassBreak");

    [ViewVariables]
    public bool Spent;
}

[RegisterComponent]
public sealed partial class ArrowInjectionResistanceComponent : Component
{
    [DataField]
    public float Resistance = 0f;
}

public sealed class GetArrowInjectionResistanceEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
    public float Resistance;
}
