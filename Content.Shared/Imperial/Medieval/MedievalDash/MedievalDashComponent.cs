using Content.Shared.Imperial.EntityLayer;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Dash;


[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalDashComponent : Component
{
    /// <summary>
    /// Force of dash
    /// </summary>
    [DataField]
    public float Force = 500.0f;

    /// <summary>
    /// Stamina damage on dash
    /// </summary>
    [DataField]
    public float StaminaDamage = 20f;

    /// <summary>
    /// Dash reload time
    /// </summary>
    [DataField]
    public TimeSpan AdditionalDashReloadTime = TimeSpan.FromSeconds(3f);

    /// <summary>
    /// Z-level of dashed entities
    /// </summary>
    [DataField]
    public EntityLayerGroups DashLayer = EntityLayerGroups.Dash;


    [ViewVariables]
    public TimeSpan NextDash = TimeSpan.Zero;

    [ViewVariables]
    public bool IsDashing = false;

    [ViewVariables]
    public HashSet<EntityLayerGroups> CachedLayers = new();
}
