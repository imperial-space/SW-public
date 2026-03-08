namespace Content.Server.Myrmex.Structures;

[RegisterComponent]
public sealed partial class MyrmexLifeSourceComponent : MyrmexPowerStructureComponent
{
    [DataField]
    public float HealthMultiplierIncrease = 0.2f;
}
