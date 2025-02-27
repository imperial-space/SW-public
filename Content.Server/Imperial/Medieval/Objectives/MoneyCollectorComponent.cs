namespace Content.Server.Imperial.Medieval;

[RegisterComponent]
public sealed partial class MoneyCollectorComponent : Component
{
    [DataField]
    public string ObjectivePrototype = "MedievalGetMoneyObjective";

    [DataField]
    public TimeSpan StartTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);
    [DataField]
    public bool Predicted = false;
}
