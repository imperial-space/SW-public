using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Trading;

[Serializable, NetSerializable]
public enum TradingUiKey : byte
{
    Key
}

[Flags, Serializable, NetSerializable]
public enum TradingMarketSection : byte
{
    Common = 1,
    Unique = 2
}

[Serializable, NetSerializable]
public enum TradingOfferSide : byte
{
    Buy,
    Sell
}

[Serializable, NetSerializable]
public enum TradingParticipantKind : byte
{
    Guild,
    Trader
}

[Serializable, NetSerializable]
public sealed class TradingMarketItemState
{
    public Guid CommodityId;
    public EntProtoId ProductEntity;
    public TradingMarketSection Sections;
    public string DisplayName;
    public string Description;
    public int? StackCount;
    public NetEntity? PreviewEntity;
    public bool IsCommonBaseline;
    public bool HasStack;
    public int BaselineStackCount;
    public bool IsDamagedEquipment;
    public int? LowestSellPrice;
    public int? HighestBuyPrice;
    public int SellOfferCount;
    public int BuyOfferCount;
    public HashSet<ProtoId<GuildTypePrototype>> Categories;

    public TradingMarketItemState(
        Guid commodityId,
        EntProtoId productEntity,
        TradingMarketSection sections,
        string displayName,
        string description,
        int? stackCount,
        NetEntity? previewEntity,
        bool isCommonBaseline,
        bool hasStack,
        int baselineStackCount,
        bool damagedEquipment,
        int? lowestSellPrice,
        int? highestBuyPrice,
        int sellOfferCount,
        int buyOfferCount,
        HashSet<ProtoId<GuildTypePrototype>> categories)
    {
        CommodityId = commodityId;
        ProductEntity = productEntity;
        Sections = sections;
        DisplayName = displayName;
        Description = description;
        StackCount = stackCount;
        PreviewEntity = previewEntity;
        IsCommonBaseline = isCommonBaseline;
        HasStack = hasStack;
        BaselineStackCount = baselineStackCount;
        IsDamagedEquipment = damagedEquipment;
        LowestSellPrice = lowestSellPrice;
        HighestBuyPrice = highestBuyPrice;
        SellOfferCount = sellOfferCount;
        BuyOfferCount = buyOfferCount;
        Categories = categories;
    }
}

[Serializable, NetSerializable]
public sealed class TradingMarketOfferState
{
    public Guid Id;
    public Guid CommodityId;
    public EntProtoId ProductEntity;
    public TradingOfferSide Side;
    public TradingParticipantKind ParticipantKind;
    public string ParticipantName;
    public int Price;
    public int ReceiptAmount;
    public bool IsOwn;
    public string DisplayName;
    public NetEntity? PreviewEntity;

    public TradingMarketOfferState(
        Guid id,
        Guid commodityId,
        EntProtoId productEntity,
        TradingOfferSide side,
        TradingParticipantKind participantKind,
        string participantName,
        int price,
        int receiptAmount,
        bool isOwn,
        string displayName,
        NetEntity? previewEntity)
    {
        Id = id;
        CommodityId = commodityId;
        ProductEntity = productEntity;
        Side = side;
        ParticipantKind = participantKind;
        ParticipantName = participantName;
        Price = price;
        ReceiptAmount = receiptAmount;
        IsOwn = isOwn;
        DisplayName = displayName;
        PreviewEntity = previewEntity;
    }
}

[Serializable, NetSerializable]
public sealed class TradingStoredItemState
{
    public NetEntity Item;
    public EntProtoId ProductEntity;
    public string DisplayName;

    public TradingStoredItemState(NetEntity item, EntProtoId productEntity, string displayName)
    {
        Item = item;
        ProductEntity = productEntity;
        DisplayName = displayName;
    }
}

[Serializable, NetSerializable]
public sealed class TradingPendingSaleState
{
    public Guid Id;
    public string ItemName;
    public string BuyerName;
    public int Price;
    public int ReceiptAmount;
    public int SellerRevenue;

    public TradingPendingSaleState(
        Guid id,
        string itemName,
        string buyerName,
        int price,
        int receiptAmount,
        int sellerRevenue)
    {
        Id = id;
        ItemName = itemName;
        BuyerName = buyerName;
        Price = price;
        ReceiptAmount = receiptAmount;
        SellerRevenue = sellerRevenue;
    }
}

/// <summary>
/// An item listed by a player on the public listing board.
/// </summary>
[Serializable, NetSerializable]
public sealed class PublicListingState
{
    public Guid Id;
    public EntProtoId ProductEntity;
    public string DisplayName;
    public int Price;
    public string SellerName;
    public bool IsOwn;
    public NetEntity? PreviewEntity;

    public PublicListingState(
        Guid id,
        EntProtoId productEntity,
        string displayName,
        int price,
        string sellerName,
        bool isOwn,
        NetEntity? previewEntity)
    {
        Id = id;
        ProductEntity = productEntity;
        DisplayName = displayName;
        Price = price;
        SellerName = sellerName;
        IsOwn = isOwn;
        PreviewEntity = previewEntity;
    }
}

/// <summary>
/// Revenue from a sold public listing, collectable at any public pit.
/// </summary>
[Serializable, NetSerializable]
public sealed class PublicPendingSaleState
{
    public Guid Id;
    public string ItemName;
    public string BuyerName;
    public int Price;

    public PublicPendingSaleState(Guid id, string itemName, string buyerName, int price)
    {
        Id = id;
        ItemName = itemName;
        BuyerName = buyerName;
        Price = price;
    }
}

/// <summary>
/// An item a player placed in a public pit but has not listed or sold yet.
/// </summary>
[Serializable, NetSerializable]
public sealed class PublicStagedItemState
{
    public Guid Id;
    public EntProtoId ProductEntity;
    public string DisplayName;
    public NetEntity? PreviewEntity;

    /// <summary>
    /// Instant-sell payout (half of the item value).
    /// </summary>
    public int InstantSellValue;

    /// <summary>
    /// Price of the best matching merchant buy order, if any.
    /// </summary>
    public int? FulfillableBuyOrderPrice;

    public PublicStagedItemState(
        Guid id,
        EntProtoId productEntity,
        string displayName,
        NetEntity? previewEntity,
        int instantSellValue,
        int? fulfillableBuyOrderPrice)
    {
        Id = id;
        ProductEntity = productEntity;
        DisplayName = displayName;
        PreviewEntity = previewEntity;
        InstantSellValue = instantSellValue;
        FulfillableBuyOrderPrice = fulfillableBuyOrderPrice;
    }
}

[Serializable, NetSerializable]
public sealed class TradingUpdateState : BoundUserInterfaceState
{
    public List<TradingMarketItemState> Items;
    public List<TradingMarketOfferState> Offers;
    public List<TradingStoredItemState> StoredItems;
    public List<TradingPendingSaleState> PendingSales;
    public List<string> Archive;
    public int Balance;
    public ProtoId<CurrencyPrototype> Currency;
    public bool IsOwner;
    public bool IsPublic;
    public List<PublicListingState> PublicListings;
    public List<PublicPendingSaleState> PublicPendingSales;
    public List<PublicStagedItemState> PublicStagedItems;
    public bool IsMerchantOnline;

    public TradingUpdateState(
        List<TradingMarketItemState> items,
        List<TradingMarketOfferState> offers,
        List<TradingStoredItemState> storedItems,
        List<TradingPendingSaleState> pendingSales,
        List<string> archive,
        int balance,
        ProtoId<CurrencyPrototype> currency,
        bool isOwner,
        bool isPublic,
        List<PublicListingState> publicListings,
        List<PublicPendingSaleState> publicPendingSales,
        List<PublicStagedItemState> publicStagedItems,
        bool isMerchantOnline)
    {
        Items = items;
        Offers = offers;
        StoredItems = storedItems;
        PendingSales = pendingSales;
        Archive = archive;
        Balance = balance;
        Currency = currency;
        IsOwner = isOwner;
        IsPublic = isPublic;
        PublicListings = publicListings;
        PublicPendingSales = publicPendingSales;
        PublicStagedItems = publicStagedItems;
        IsMerchantOnline = isMerchantOnline;
    }
}

[Serializable, NetSerializable]
public sealed class TradingUpdateInterfaceMessage(TradingUpdateState state) : BoundUserInterfaceMessage
{
    public TradingUpdateState State = state;
}

[Serializable, NetSerializable]
public sealed class TradingRequestUpdateInterfaceMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TradingBuyMessage(Guid commodityId) : BoundUserInterfaceMessage
{
    public Guid CommodityId = commodityId;
}

[Serializable, NetSerializable]
public sealed class TradingSellMessage(Guid commodityId) : BoundUserInterfaceMessage
{
    public Guid CommodityId = commodityId;
}

[Serializable, NetSerializable]
public sealed class TradingBuyOfferMessage(Guid offerId) : BoundUserInterfaceMessage
{
    public Guid OfferId = offerId;
}

[Serializable, NetSerializable]
public sealed class TradingSellOfferMessage(Guid offerId) : BoundUserInterfaceMessage
{
    public Guid OfferId = offerId;
}

[Serializable, NetSerializable]
public sealed class TradingSelectCommodityMessage(Guid commodityId) : BoundUserInterfaceMessage
{
    public Guid CommodityId = commodityId;
}

[Serializable, NetSerializable]
public sealed class TradingSelectOfferMessage(Guid offerId) : BoundUserInterfaceMessage
{
    public Guid OfferId = offerId;
}

[Serializable, NetSerializable]
public sealed class TradingCreateSellOfferMessage(int price) : BoundUserInterfaceMessage
{
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class TradingPrepareUnitSellOfferMessage(int price) : BoundUserInterfaceMessage
{
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class TradingUnitSellOfferPreparedMessage(
    Guid requestId,
    string itemName,
    int price,
    int maximumAmount) : BoundUserInterfaceMessage
{
    public Guid RequestId = requestId;
    public string ItemName = itemName;
    public int Price = price;
    public int MaximumAmount = maximumAmount;
}

[Serializable, NetSerializable]
public sealed class TradingCreateUnitSellOffersMessage(Guid requestId, int amount) : BoundUserInterfaceMessage
{
    public Guid RequestId = requestId;
    public int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class TradingCreateBuyOfferMessage(Guid commodityId, int price) : BoundUserInterfaceMessage
{
    public Guid CommodityId = commodityId;
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class TradingCreateBuyOfferFromHeldMessage(int price) : BoundUserInterfaceMessage
{
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class TradingCancelOfferMessage(Guid offerId) : BoundUserInterfaceMessage
{
    public Guid OfferId = offerId;
}

[Serializable, NetSerializable]
public sealed class TradingCreateBidReceiptMessage(Guid offerId, int amount) : BoundUserInterfaceMessage
{
    public Guid OfferId = offerId;
    public int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class TradingCollectStoredItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class TradingCollectSaleRevenueMessage(Guid saleId) : BoundUserInterfaceMessage
{
    public Guid SaleId = saleId;
}

[Serializable, NetSerializable]
public sealed class TradingExamineItemMessage(NetEntity item) : BoundUserInterfaceMessage
{
    public NetEntity Item = item;
}

[Serializable, NetSerializable]
public sealed class TradingExamineCommodityMessage(Guid commodityId) : BoundUserInterfaceMessage
{
    public Guid CommodityId = commodityId;
}

[Serializable, NetSerializable]
public sealed class TradingExamineInfoMessage(
    NetEntity item,
    FormattedMessage message,
    List<Verb> verbs,
    EntProtoId? previewProduct = null,
    Guid? commodityId = null) : BoundUserInterfaceMessage
{
    public NetEntity Item = item;
    public FormattedMessage Message = message;
    public List<Verb> Verbs = verbs;
    public EntProtoId? PreviewProduct = previewProduct;
    public Guid? CommodityId = commodityId;
}

[Serializable, NetSerializable]
public sealed class TradingExecuteExamineVerbMessage(
    NetEntity item,
    ExamineVerb requestedVerb) : BoundUserInterfaceMessage
{
    public NetEntity Item = item;
    public ExamineVerb RequestedVerb = requestedVerb;
}

[Serializable, NetSerializable]
public sealed class TradingRequestWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class TradingBuyPublicListingMessage(Guid listingId) : BoundUserInterfaceMessage
{
    public Guid ListingId = listingId;
}

[Serializable, NetSerializable]
public sealed class TradingCancelPublicListingMessage(Guid listingId) : BoundUserInterfaceMessage
{
    public Guid ListingId = listingId;
}

[Serializable, NetSerializable]
public sealed class TradingListStagedItemMessage(Guid stagedItemId, int price) : BoundUserInterfaceMessage
{
    public Guid StagedItemId = stagedItemId;
    public int Price = price;
}

[Serializable, NetSerializable]
public sealed class TradingSellStagedItemMessage(Guid stagedItemId) : BoundUserInterfaceMessage
{
    public Guid StagedItemId = stagedItemId;
}

[Serializable, NetSerializable]
public sealed class TradingFulfillStagedItemMessage(Guid stagedItemId) : BoundUserInterfaceMessage
{
    public Guid StagedItemId = stagedItemId;
}

[Serializable, NetSerializable]
public sealed class TradingWithdrawStagedItemMessage(Guid stagedItemId) : BoundUserInterfaceMessage
{
    public Guid StagedItemId = stagedItemId;
}

[Serializable, NetSerializable]
public sealed class TradingCollectPublicSaleRevenueMessage(Guid saleId) : BoundUserInterfaceMessage
{
    public Guid SaleId = saleId;
}
