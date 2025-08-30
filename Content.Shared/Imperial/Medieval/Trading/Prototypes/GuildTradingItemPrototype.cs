using System.Linq;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

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

    /// <summary>
    /// Минимальное количество репутации для покупки этого предмета
    /// </summary>
    [DataField]
    public int MinReputation = 0;

    /// <summary>
    /// Минимальное место в таблице репутации гильдии.
    /// Например, на первом месте игрок, у которого больше всего репутации с данной гильдией.
    /// По умолчанию 0 - любое место
    /// </summary>
    [DataField]
    public int MinReputationPlace = 0;

    [DataField]
    public int ReputationForBuying = 2;
    public Guid GuildId;

    [DataField] public string? Name;
    [DataField] public string? Description;

    public bool CanBuy(NetEntity ent, Guild guild)
    {
        var reputation = guild.GetReputation(ent);

        if (reputation < MinReputation)
            return false;

        if (MinReputationPlace > 0)
        {
            var sorted = guild.Reputation
                .OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .Take(MinReputationPlace)
                .ToList();

            if (!sorted.Contains(ent))
                return false;
        }

        return true;
    }


    public bool Equals(GuildTradingItem? other)
    {
        if (other == null)
            return false;

        return ProductEntity == other.ProductEntity &&
               Cost == other.Cost &&
               MinReputation == other.MinReputation &&
               MinReputationPlace == other.MinReputationPlace;
    }
}
