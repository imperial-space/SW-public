using Content.Shared.Dataset;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.RemoteStore;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype("randomStorePreset")]
public sealed partial class RandomStorePresetPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> StoreNames;

    [DataField(required: true)]
    public HashSet<ProtoId<StoreCategoryPrototype>> Categories = [];

    [DataField]
    public FixedPoint2 PriceRandomModifier = 0.2f;
}
