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
    public float Force = 1000.0f;

    /// <summary>
    /// Stamina damage on dash
    /// </summary>
    [DataField]
    public float StaminaDamage = 26f;

    /// <summary>
    /// Dash reload time
    /// </summary>
    [DataField]
    public TimeSpan AdditionalDashReloadTime = TimeSpan.Zero;

    /// <summary>
    /// Z-level of dashed entities
    /// </summary>
    [DataField]
    public EntityLayerGroups DashLayer = EntityLayerGroups.Dash;


    [ViewVariables]
    public TimeSpan NextDash = TimeSpan.Zero;

    [ViewVariables]
    public HashSet<EntityLayerGroups> CachedLayers = new();
}
