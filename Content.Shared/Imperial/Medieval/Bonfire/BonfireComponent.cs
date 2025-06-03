using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.Medieval.Bonfire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BonfireComponent : Component
{
    [DataField]
    public string IgnitionSound = "/Audio/Items/Flare/flare_on.ogg";

    [DataField]
    public string ExtinguishSound = "/Audio/Items/candle_blowing.ogg";

    [DataField, AutoNetworkedField]
    public BonfireVisuals IsLit = BonfireVisuals.Off;

    [DataField]
    public float MaxFuel = 100f;

    [DataField, AutoNetworkedField]
    public float CurrentFuel = 0f;

    [DataField]
    public float HeatingPower = 1200f;
}

[Serializable, NetSerializable]
public sealed partial class IgnitionDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public enum BonfireVisuals : byte
{
    Off,
    Fire
}

[Serializable, NetSerializable]
public enum BonfireVisualLayers : byte
{
    Bonfire,
    Fire
}
