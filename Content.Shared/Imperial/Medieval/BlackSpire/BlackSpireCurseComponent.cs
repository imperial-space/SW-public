using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.BlackSpire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlackSpireCurseComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1.3f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(5);

    public bool Active = true;

    public float LastHunger = float.NaN;

    public float LastThirst = float.NaN;
}
