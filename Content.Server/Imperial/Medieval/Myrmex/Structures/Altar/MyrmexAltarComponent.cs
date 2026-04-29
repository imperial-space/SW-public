namespace Content.Server.Myrmex.Structures;

[RegisterComponent]
public sealed partial class MyrmexAltarComponent : MyrmexPowerStructureComponent
{
    [DataField]
    public int BuffsIncrease = 5;
}
