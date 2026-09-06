using Content.Shared.FixedPoint;
using Content.Shared.Imperial.ImperialStore;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;

[RegisterComponent]
public sealed partial class GrimoireOwnerComponent : Component
{
    [ViewVariables]
    public EntityUid GrimoireUid;

    [ViewVariables]
    public EntProtoId GrimoirePrototype;

    [ViewVariables]
    public Dictionary<string, FixedPoint2> Balance = [];

    [ViewVariables]
    public bool BonusBalanceOverride;

    [ViewVariables]
    public int LastBonusIndex;

    [ViewVariables]
    public Dictionary<string, FixedPoint2>[] Bonuses = [];

    [ViewVariables]
    public Dictionary<string, FixedPoint2> BonusSum = [];

    [ViewVariables]
    public HashSet<ProtoId<ImperialStoreCategoryPrototype>> Categories = [];

    [ViewVariables]
    public HashSet<ProtoId<ImperialCurrencyPrototype>> CurrencyWhitelist = [];

    [ViewVariables]
    public HashSet<ImperialListingData> Listings = [];

    [ViewVariables]
    public List<EntityUid> BoughtEntities = [];

    [ViewVariables]
    public Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2> BalanceSpent = [];

    [ViewVariables]
    public bool RefundAllowed;

    [ViewVariables]
    public bool OwnerOnly;

    [ViewVariables]
    public EntityUid? StartingMap;
}
