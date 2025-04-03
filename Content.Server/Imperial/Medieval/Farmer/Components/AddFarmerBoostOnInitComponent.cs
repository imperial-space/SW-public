namespace Content.Server.Imperial.Medieval.Farmer;

[RegisterComponent]
public sealed partial class AddFarmerBoostOnInitComponent : Component
{
    [DataField]
    public float Range = 3f;
}
