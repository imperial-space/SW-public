namespace Content.Server.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class AlchemyCoolingBathComponent : Component
{
    [DataField] public float MinimumTemperature = 263.15f;
    [DataField] public float CoolingPower = 100f;
    [DataField] public List<int> Durations = new() { 5, 10, 15, 30 };
    [DataField] public int SelectedDuration = 5;
    public float RemainingTime;
    public bool IsProcessing;
}
