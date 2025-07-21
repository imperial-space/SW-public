namespace Content.Server.ShiftFront.Components;

[RegisterComponent]
public sealed partial class ShiftResourceExtractComponent : Component
{
    [DataField]
    public int Polymer = 10;

    [DataField]
    public int BioShlak = 10;

    [DataField]
    public int NanoCarbon = 0;

}
