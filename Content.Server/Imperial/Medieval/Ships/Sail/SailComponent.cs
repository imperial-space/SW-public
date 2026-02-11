namespace Content.Server.Imperial.Medieval.Ships.Sail;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SailComponent : Component
{
    [DataField("SailSize")]
    public int SailSize = 1;

    [DataField("Folded")]
    public bool Folded;
}
