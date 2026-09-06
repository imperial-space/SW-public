using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class TradingMarketComponent : Component
{
    public Dictionary<Guid, TradingCommodity> Commodities = new();
    public Dictionary<EntProtoId, Guid> CommonCommodities = new();
    public List<Guild> Guilds = new();
    public Dictionary<Guid, TradingMarketOffer> Offers = new();
    public int NextSequence;
    public ProtoId<TradingMarketConfigPrototype> Config = "MedievalMarket";
    public float PriceWeightBase = 0.5f;
}

public sealed class TradingCommodity
{
    public Guid Id;
    public EntProtoId Product;
    public TradingMarketSection Sections;
    public int StandardPrice;
    public int MinReputation;
    public int InitialScarcitySteps;
    public int RemainingScarcitySteps;
    public int BaselineStackCount = 1;
    public bool HasStack;
    public bool Permanent;
    public bool IsDamagedEquipment;
    public string Signature = string.Empty;
    public string DisplayName = string.Empty;
    public string Description = string.Empty;
    public HashSet<ProtoId<GuildTypePrototype>> Categories = new();
    public TradingOrderBook BuyBook = new();
    public TradingOrderBook SellBook = new();
}

public sealed class TradingOrderBook
{
    public Dictionary<int, int> PriceLevels = new();
    public int GuildOfferCount;
    public TradingPriceAggregate Prices = new();
}

public sealed class TradingPriceAggregate
{
    public int Count;
    public float AveragePrice = float.NaN;
    public float ReferencePrice;
    public float PriceWeightBase;
    public float MaximumLogWeight = float.NegativeInfinity;
    public double ScaledWeightSum;
    public double ScaledPriceSum;
}

public sealed class TradingMarketOffer
{
    public Guid Id;
    public Guid CommodityId;
    public EntProtoId Product;
    public TradingOfferSide Side;
    public TradingParticipantKind ParticipantKind;
    public string ParticipantName = string.Empty;
    public int Price;
    public Guid? GuildId;
    public EntityUid? Pit;
    public EntityUid? ImmediateRecipient;
    public EntityUid? Item;
    public string ListedItemName = string.Empty;
    public bool IsImmediate;
    public bool UsesExternalFunds;
    public int Sequence;
}

[RegisterComponent]
public sealed partial class TradingMarketViewerComponent : Component
{
    public HashSet<EntityUid> VisibleItems = new();
    public Dictionary<Guid, EntityUid> ExaminePreviewItems = new();
    public Guid? SelectedCommodity;
    public Guid? SelectedOffer;
}
