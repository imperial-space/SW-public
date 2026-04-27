using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ObeliskFixer;

[RegisterComponent, NetworkedComponent]
public sealed partial class ObeliskFixerComponent : Component
{
    [DataField]
    public float BaseDoAfterDuration = 20f;

    [DataField]
    public int BaselineIntelligence = 10;

    [DataField]
    public float IntelligenceDurationModifier = 1.5f;

    [DataField]
    public float MinimumDoAfterDuration = 0.01f;
}

[Serializable, NetSerializable]
public sealed partial class ObeliskFixerDoAfterEvent : SimpleDoAfterEvent;
