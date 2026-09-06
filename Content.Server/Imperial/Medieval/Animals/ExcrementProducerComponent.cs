using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Animals;

/// <summary>
/// A component that allows the entity to produce excrements (manure) at a specific interval
/// </summary>
[RegisterComponent]
public sealed partial class ExcrementProducerComponent: Component
{
    /// <summary>
    /// The minimum interval at which the excrements are produced
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan ProduceMinimumInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The percent of chance the excrement will be produced at the minimum interval
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ProduceChance = 0.15f;

    /// <summary>
    /// The entity of the product
    /// </summary>
    /// <summary>
    ///     The item that gets excreted, retrieved from animal prototype.
    /// </summary>
    [DataField(required: true)]
    public List<EntitySpawnEntry> ExcrementSpawn = new();

    /// <summary>
    /// The last time the excrement was produced
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan LastProducedTime;
}
