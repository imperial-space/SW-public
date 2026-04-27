namespace Content.Server.Imperial.Medieval.Ships.Sea.Init;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class MapForSeasInitComponent : Component
{
    [DataField("mapX")]
    public int MapX { get; set; }
    [DataField("mapY")]
    public int MapY { get; set; }
}
