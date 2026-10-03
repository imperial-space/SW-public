using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Salary;

/// <summary>
/// Pays out the whole salary balance of whoever activates it.
/// </summary>
[RegisterComponent]
public sealed partial class MedievalAtmComponent : Component
{
    [DataField]
    public EntProtoId Cash = "MedievalRevent";

    [DataField]
    public SoundSpecifier? WithdrawSound;
}
