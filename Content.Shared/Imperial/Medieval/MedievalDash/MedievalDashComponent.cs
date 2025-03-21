using Content.Shared.Imperial.EntityLayer;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Dash;


[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalDashComponent : Component
{
    /// <summary>
    /// Force of dash
    /// </summary>
    [DataField]
    public float Force = 760.0f;

    /// <summary>
    /// Stamina damage on dash
    /// </summary>
    [DataField]
    public float StaminaDamage = 26f;

    /// <summary>
    /// Dash reload time
    /// </summary>
    [DataField]
    public TimeSpan DashReloadTime = TimeSpan.FromSeconds(2f);

    /// <summary>
    /// Z-level of dashed entities
    /// </summary>
    [DataField]
    public EntityLayerGroups DashLayer = EntityLayerGroups.Dash;


    [ViewVariables]
    public bool IsDashing = false;

    [ViewVariables]
    public TimeSpan NextDash = TimeSpan.Zero;

    [ViewVariables]
    public TimeSpan DashEndTime = TimeSpan.Zero;

    [ViewVariables]
    public GameTick DashButtonPressedTick;

    [ViewVariables]
    public HashSet<EntityLayerGroups> CachedLayers = new();
}
