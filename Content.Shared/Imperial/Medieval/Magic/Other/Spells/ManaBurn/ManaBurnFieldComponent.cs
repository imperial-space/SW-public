namespace Content.Shared.Imperial.Medieval.Magic.ManaBurn;


[RegisterComponent]
public sealed partial class ManaBurnFieldComponent : Component
{
    [DataField]
    public TimeSpan BurnDelay;
    [DataField]
    public float BurnQuantity = 5;
    [DataField]
    public float Radius = 3f;
    [ViewVariables]
    public TimeSpan BurnTime = TimeSpan.Zero;
    [DataField]
    public LocId BurnPopup;
}
