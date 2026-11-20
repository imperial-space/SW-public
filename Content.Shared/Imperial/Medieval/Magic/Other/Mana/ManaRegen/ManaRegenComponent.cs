using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Magic.ManaRegen;


[RegisterComponent, NetworkedComponent]
public sealed partial class ManaRegenComponent : Component
{
    [DataField]
    public float Regen = 0.5f;

    [DataField]
    public LocId ManaRegenMessage = "medieval-mana-regen";

    [DataField]
    public EntityUid? Equipee;

    // This has been replaced with a global timer.
    /*[DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(1f);


    [ViewVariables]
    public TimeSpan EndTime = TimeSpan.FromSeconds(0f);*/

}
