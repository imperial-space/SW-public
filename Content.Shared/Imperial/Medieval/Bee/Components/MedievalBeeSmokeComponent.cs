namespace Content.Shared.Imperial.Medieval.Bee.Components;

[RegisterComponent]
public sealed partial class MedievalBeeSmokeComponent : Component
{
    [DataField]
    public TimeSpan PacifyTime = TimeSpan.FromSeconds(120);
    [DataField("uses")]
    public int UsesLeft = 3;
}
