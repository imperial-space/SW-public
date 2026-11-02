using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.MeleeParry.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeParryStorageComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan NextParryTime = TimeSpan.Zero;

    [DataField, AutoNetworkedField]
    public float CooldownParry = 0.5f;

    [DataField, AutoNetworkedField]
    public bool ParryQueued = false;

    /// <summary>Reject repeated or backward-moving parry requests on this actor.</summary>
    // Runtime input history is networked for prediction, but is not prototype data.
    // GameTick has no prototype copy serializer in this engine version.
    [ViewVariables, AutoNetworkedField]
    public GameTick LastParryInputTick;
}
