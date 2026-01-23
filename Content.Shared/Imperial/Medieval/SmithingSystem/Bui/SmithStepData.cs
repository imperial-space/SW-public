using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.SmithingSystem.Bui;

[Serializable, NetSerializable]
public sealed class SmithStepData
{
    public SmithHitState State;
    public bool IsPenaltyActivator;
    public float PerfectHitTime;
    public float GoodHitTime;
}
