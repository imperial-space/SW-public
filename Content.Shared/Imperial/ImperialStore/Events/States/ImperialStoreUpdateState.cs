using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ImperialStore;


[Serializable, NetSerializable]
public sealed class ImperialStoreUpdateState(HashSet<ImperialListingData> listings, Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2> balance, bool showFooter, bool allowRefund, List<string?>? slots = null, List<string>? selectableSchools = null) : BoundUserInterfaceState
{
    public readonly HashSet<ImperialListingData> Listings = listings;

    public readonly Dictionary<ProtoId<ImperialCurrencyPrototype>, FixedPoint2> Balance = balance;

    public readonly bool ShowFooter = showFooter;

    public readonly bool AllowRefund = allowRefund;

    // imperial medieval - essence slots: the school in every slot (null is empty) and the schools that can be put into a slot now
    public readonly List<string?> Slots = slots ?? [];

    public readonly List<string> SelectableSchools = selectableSchools ?? [];
}
