using Content.Server.Imperial.Medieval.RemoteStore;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.TradeGuildsRule;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class TradeGuildsRuleComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<RandomStorePresetPrototype>, uint> Guilds = [];
}
