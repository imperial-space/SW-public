namespace Content.Server.Imperial.Medieval.Ships.Sea.Init;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class NotSeaComponent : Component
{
    [DataField("NotSeaPosX")]
    public int NotSeaPosX;

    [DataField("NotSeaPosY")]
    public int NotSeaPosY;
}
