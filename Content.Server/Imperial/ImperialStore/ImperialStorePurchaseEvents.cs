using Content.Shared.Imperial.ImperialStore;

namespace Content.Server.Imperial.ImperialStore;

[ByRefEvent]
public readonly record struct ImperialStoreRefreshListingsEvent(EntityUid Buyer, ImperialStoreComponent Store);
[ByRefEvent]
public record struct ImperialStorePurchaseAttemptEvent(EntityUid Buyer, ImperialListingData Listing, bool Cancelled = false);
[ByRefEvent]
public readonly record struct ImperialStorePurchasedEvent(EntityUid Buyer, ImperialListingData Listing);
