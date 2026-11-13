// configurable cavalry lance normal attacks.
namespace Content.Shared.Imperial.Medieval.Lances;

[RegisterComponent]
public sealed partial class LanceMeleeComponent : Component
{
    [DataField]
    public int MinimumOneHandStrength = 16;

    [DataField]
    public int TwoHandedPoiseBonus = 8;
}
