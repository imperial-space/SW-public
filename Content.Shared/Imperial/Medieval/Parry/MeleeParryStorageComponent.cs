using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.MeleeParry.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeParryStorageComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan GlobalNextParryTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public float GlobalCooldownParry = 0.5f;
}
