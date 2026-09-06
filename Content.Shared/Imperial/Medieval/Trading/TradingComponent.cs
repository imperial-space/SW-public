using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Trading;

[RegisterComponent, NetworkedComponent]
public sealed partial class TradingComponent : Component
{
    public const string MarketContainerId = "trading-pit-market";

    [DataField]
    public int Balance;

    [DataField]
    public ProtoId<CurrencyPrototype> Currency;

    [DataField]
    public EntityUid? AccountOwner = null;

    [DataField]
    public HashSet<ProtoId<GuildTypePrototype>> GuildTypes;

    public HashSet<Guid> MarketOffers = new();

    public List<EntityUid> StoredMarketItems = new();

    public List<TradingPendingSale> PendingSales = new();

    public List<string> MarketArchive = new();

    [DataField]
    public SoundSpecifier BuySuccessSound = new SoundPathSpecifier("/Audio/Effects/kaching.ogg");
}

public sealed class TradingPendingSale
{
    public Guid Id;
    public string ItemName = string.Empty;
    public string BuyerName = string.Empty;
    public int Price;
}
