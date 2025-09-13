namespace Content.Shared.Imperial.Medieval.Magic.ManaBurn;


[RegisterComponent]
public sealed partial class ManaBurnFieldComponent : Component
{
    [ViewVariables]
    public TimeSpan burnDelay = TimeSpan.FromSeconds(5);
    [DataField]
    public float burnQuantity = 5;
    [DataField]
    public float radius = 3f;
    [ViewVariables]
    public TimeSpan burnTime = TimeSpan.FromSeconds(0);
    [DataField]
    public LocId burnPopup;
}
