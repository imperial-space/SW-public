using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public enum MedievalAnchorVisuals : byte
{
    Lowered
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalAnchorComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Lowered;

    [DataField]
    public float BaseUseTime = 11f;

    [DataField]
    public float StrengthUseTimeModifier = 0.3f;

    [DataField]
    public float LoweringTimeMultiplier = 0.1f;

    [DataField]
    public float IslandSearchRange = 25f;

    [DataField]
    public float WaveDisableDelay = 120f;

    public TimeSpan? WavesDisabledAt;

    [AutoNetworkedField]
    public EntityUid? ActiveUser;
}
