namespace Content.Shared.Imperial.Medieval.Magic.ManaBurn;


[RegisterComponent]
public sealed partial class ManaBurnFieldComponent : Component
{
    [ViewVariables]
    public TimeSpan BurnDelay = TimeSpan.FromSeconds(5);
    [DataField]
    public float BurnQuantity = 5;
    [DataField]
    public float Radius = 3f;
    [ViewVariables]
    public TimeSpan BurnTime = TimeSpan.FromSeconds(0);
    [DataField]
    public LocId BurnPopup;
}
