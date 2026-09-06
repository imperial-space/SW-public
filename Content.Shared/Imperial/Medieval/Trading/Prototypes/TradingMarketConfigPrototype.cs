using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Trading.Prototypes;

[Prototype]
public sealed partial class TradingMarketConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public HashSet<ProtoId<GuildTypePrototype>> GuildTypes = new();

    [DataField]
    public HashSet<ProtoId<TagPrototype>> BlockedTraderItemTags = new();

    [DataField]
    public float StepInterval = 30f;

    [DataField]
    public float ReputationScarcityMinutesPerPoint = 3f;

    [DataField]
    public int LiquidityReferencePrice = 300;

    [DataField]
    public int LiquidityReferenceOfferCount = 20;

    [DataField]
    public int MinimumGuildOfferCount = 10;

    [DataField]
    public int MaximumGuildOfferCount = 40;

    [DataField]
    public float InitialGuildPriceSpread = 0.12f;

    [DataField]
    public float InitialGuildPriceDepth = 0.18f;

    [DataField]
    public int MaximumGuildSellOfferCount = 200;

    [DataField]
    public int MaximumGuildBuyOrderCount = 100;

    [DataField]
    public float InterventionChanceScale = 0.4f;

    [DataField]
    public float InterventionCorrectionStrength = 0.25f;

    [DataField]
    public float GuildOfferRemovalChanceScale = 0.01f;
}
