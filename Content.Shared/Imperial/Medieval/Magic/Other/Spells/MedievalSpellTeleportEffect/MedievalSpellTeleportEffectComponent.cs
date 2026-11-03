using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Magic.MedievalSpellTeleportEffect;


/// <summary>
/// Add phase space components on caster
/// </summary>
[RegisterComponent]
public sealed partial class MedievalSpellTeleportEffectComponent : Component
{
    [DataField]
    public ComponentRegistry FadeDistortion = new();

    [DataField]
    public ComponentRegistry AppearanceDistortion = new();

    [DataField]
    public bool CheckOccluded = true;

    /// <summary>
    /// Hide the caster sprite while the spell is being cast.
    /// </summary>
    [DataField]
    public bool HideCaster = true;
}

