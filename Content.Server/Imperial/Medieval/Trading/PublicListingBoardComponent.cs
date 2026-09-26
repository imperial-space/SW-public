using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

/// <summary>
/// Player listings shared by all public trading pits.
/// </summary>
[RegisterComponent]
public sealed partial class PublicListingBoardComponent : Component
{
    public const string ListingContainerId = "public-listing-board";
    public const string StagingContainerId = "public-listing-staging";

    public Dictionary<Guid, PublicListing> Listings = new();

    public Dictionary<EntityUid, List<PublicPendingSale>> PendingSalesByMind = new();

    /// <summary>
    /// Items inserted into a pit but not yet listed or sold.
    /// </summary>
    public Dictionary<Guid, PublicStagedItem> StagedItems = new();

    public int NextSequence;
}

public sealed class PublicListing
{
    public Guid Id;
    public EntityUid Item;
    public EntProtoId Product;
    public string DisplayName = string.Empty;
    public int Price;
    public EntityUid SellerMindId;
    public string SellerName = string.Empty;
    public int Sequence;
}

public sealed class PublicPendingSale
{
    public Guid Id;
    public string ItemName = string.Empty;
    public string BuyerName = string.Empty;
    public int Price;
}

public sealed class PublicStagedItem
{
    public Guid Id;
    public EntityUid Item;
    public EntProtoId Product;
    public string DisplayName = string.Empty;
    public EntityUid OwnerMindId;
    public string SellerName = string.Empty;
    public int Sequence;
}
