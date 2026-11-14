namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossVigilFeatureSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<BossMechanicEvent>(EntityManager.System<BossMechanicSystem>().OnVigilMechanic);
}
