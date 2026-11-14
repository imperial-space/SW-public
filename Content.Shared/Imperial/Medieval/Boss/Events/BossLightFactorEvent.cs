namespace Content.Shared.Imperial.Medieval.Boss;

[ByRefEvent]
public struct BossLightFactorEvent(EntityUid entity)
{
    public readonly EntityUid Entity = entity;
    public float Factor = 1;
}
