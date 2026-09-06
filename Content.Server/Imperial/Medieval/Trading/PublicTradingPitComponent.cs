using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class PublicTradingPitComponent : Component;

[RegisterComponent]
public sealed partial class PublicTradingBalanceComponent : Component
{
    public HashSet<EntityUid> OpenPits = new();
    public Dictionary<ProtoId<CurrencyPrototype>, int> Balances = new();
    public HashSet<EntityUid> TrackedCurrencyStacks = new();
    public bool BalanceDirty;
    public bool RefreshQueued;
}

[RegisterComponent]
public sealed partial class PublicTradingCurrencyTrackerComponent : Component
{
    public EntityUid User;
}
