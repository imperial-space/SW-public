namespace Content.Server.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class TradingUnitSellRequestComponent : Component
{
    public Guid RequestId;
    public EntityUid Pit;
    public int Price;
    public List<TradingUnitSellCandidate> Candidates = new();
}

public sealed class TradingUnitSellCandidate
{
    public EntityUid Item;
    public string Signature = string.Empty;
    public int Amount;
}
