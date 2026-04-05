using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Cult;

[Prototype]
public sealed partial class BloodSpellPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> Incantation = new();

    [DataField(required: true)]
    public BloodSpellType SpellType = BloodSpellType.ItemUpgrade;

    [DataField]
    public string? RequiredMaterial;

    [DataField]
    public int RequiredMaterialCount = 5;

    [DataField]
    public EntProtoId? SpawnProto;

    [DataField]
    public EntProtoId? RequiredHeldItem;

    [DataField]
    public EntProtoId? ReplacementItem;

    [DataField]
    public string? EquipmentSlot;

    [DataField]
    public bool ReplaceEquipment;

    [DataField("successMessage")]
    public string? SuccessMessage;

    [DataField]
    public string? FailureMessage;
}

public enum BloodSpellType
{
    ItemUpgrade,
    CraftItem,
    CraftArmor,
    DeathCurse,
}
