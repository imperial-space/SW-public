namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

[RegisterComponent]
public sealed partial class DeleteOnLowScoreOnSmithCompleteComponent : Component
{
    [DataField(required: true)]
    public int Threshold;
}
