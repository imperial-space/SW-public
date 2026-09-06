using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Trading.Prototypes;

[DataDefinition, NetSerializable, Serializable]
public sealed partial record GuildTradingItem
{
    [DataField]
    public int Cost;

    public bool IsCostChanged;
    public int ChangedCost;

    [DataField]
    public EntProtoId? ProductEntity;

    [DataField]
    public int MinReputation;

    [DataField]
    public int MinReputationPlace;

    [DataField]
    public string? SpawnOnActionWhitelist;

    [DataField]
    public float ReputationForBuying = 2;

    public Guid GuildId;

    [DataField]
    public string? Name;

    [DataField]
    public string? Description;

    public bool Equals(GuildTradingItem? other)
    {
        if (other == null)
            return false;

        return ProductEntity == other.ProductEntity &&
               Cost == other.Cost &&
               MinReputation == other.MinReputation &&
               MinReputationPlace == other.MinReputationPlace;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProductEntity, Cost, MinReputation, MinReputationPlace);
    }
}
