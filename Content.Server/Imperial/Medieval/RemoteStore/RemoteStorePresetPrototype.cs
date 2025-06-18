using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.RemoteStore;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype("tradeGuildPreset")]
public sealed partial class RemoteStorePresetPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public FixedPoint2 PriceRandomModifier = 0.2f;
}
