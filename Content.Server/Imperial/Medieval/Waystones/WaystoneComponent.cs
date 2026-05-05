using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

[RegisterComponent]
public sealed partial class WaystoneComponent : Component
{
    [DataField]
    public string Name = "Путеводный камень";

    [DataField]
    public ProtoId<MedievalFactionPrototype>? Faction;

    [DataField]
    public string LinkId = string.Empty;

    [DataField]
    public float TimeToTeleport = 5f;

    [DataField]
    public int PriceIn = 5;
    [DataField]
    public int PriceOut = 15;

    [DataField]
    public bool IsEnable = true;

    [DataField]
    public EntityUid SelectedWaystone = EntityUid.Invalid;

    [DataField]
    public int CurrentPaid = 0;

    [DataField]
    public EntityUid User = EntityUid.Invalid;

    [DataField]
    public TimeSpan BookedTime = TimeSpan.Zero;

    [DataField]
    public int collectedMoney = 0;

}
