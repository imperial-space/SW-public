using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

[Serializable, NetSerializable]
public sealed class SmithStepData
{
    public SmithHitState State;

    public float PerfectHitTime;
    public float GoodHitTime;
}
