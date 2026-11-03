namespace Content.Shared.Imperial.Medieval.Magic.MedievalActionCooldownOnDespawn;


/// <summary>
/// Starts the cooldown of the action that spawned this entity only when the entity is gone,
/// so the cooldown does not tick while the spell effect is still active
/// </summary>
[RegisterComponent]
public sealed partial class MedievalActionCooldownOnDespawnComponent : Component
{
    /// <summary>
    /// Cooldown that starts after the entity despawns
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Cooldown;

    /// <summary>
    /// The action that spawned this entity. Filled in when the spell spawns it
    /// </summary>
    [ViewVariables]
    public EntityUid? Action;
}
