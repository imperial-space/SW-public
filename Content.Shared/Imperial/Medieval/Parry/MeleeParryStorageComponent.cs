using Robust.Shared.GameStates;

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

    /// <summary>
    /// Last accepted input, restored with authoritative state during prediction.
    /// The engine's predictive event queue owns timing; this ID only rejects replays.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public uint LastParryInputId;
}

