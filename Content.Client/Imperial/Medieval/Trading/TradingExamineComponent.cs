namespace Content.Client.Imperial.Medieval.Trading;

[RegisterComponent]
[Access(typeof(TradingExamineSystem))]
public sealed partial class TradingExamineComponent : Component
{
    public EntityUid Pit;
    public EntityUid Target;
    public Guid? CommodityId;
}

[RegisterComponent]
[Access(typeof(TradingExamineSystem))]
public sealed partial class TradingExamineTargetComponent : Component
{
    public EntityUid Examiner;
}
