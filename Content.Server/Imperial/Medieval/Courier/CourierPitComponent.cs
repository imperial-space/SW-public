using Content.Shared.Imperial.Medieval.Courier;
using Content.Shared.EntityTable;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Courier;

[RegisterComponent, ComponentProtoName("courierPitComponent")]
public sealed partial class CourierPitComponent : Component
{
    [DataField]
    public string GuildName = "courier-ui-guild-name";

    [DataField]
    public List<CourierTradeOffer> Offers = new();

    [DataField]
    public int MinRewardMinutes = 5;

    [DataField]
    public int MaxRewardMinutes = 20;

    [DataField]
    public ProtoId<CurrencyPrototype> Currency = "Revent";

    [DataField]
    public TimeSpan NextRewardTime = TimeSpan.Zero;

    [DataField]
    public Dictionary<EntityUid, int> Weight = new();

    [DataField]
    public ProtoId<EntityTablePrototype> MailLootTable = "MailLootTable";

    [DataField]
    public ProtoId<EntityTablePrototype> BoxMailLootTable = "BoxMailLootTable";

    [DataField]
    public int MinDeliveryPointsReward;

    [DataField]
    public int MaxDeliveryPointsReward;

    [DataField]
    public int MinBalanceReward;

    [DataField]
    public int MaxBalanceReward;

    [DataField]
    public int UrgentBalanceRewardMultiplier;

    [DataField]
    public int BoxBalanceRewardMultiplier;

    [DataField]
    public int MinUrgentMinutes;

    [DataField]
    public int MaxUrgentMinutes;
}
