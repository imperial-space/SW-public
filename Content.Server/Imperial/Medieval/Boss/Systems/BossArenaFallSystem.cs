namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaFallSystem : EntitySystem
{
    public override void Initialize()
    {
        EntityManager.System<Content.Shared.Imperial.Medieval.Boss.BossFeatureSystem>().Register("arena-entrance-return");
        var maps = EntityManager.System<BossArenaMapSystem>();
        maps.InitializeFalls();
        SubscribeLocalEvent<BossArenaPlayerEvent>(maps.OnFallPlayerChanged);
    }
}
