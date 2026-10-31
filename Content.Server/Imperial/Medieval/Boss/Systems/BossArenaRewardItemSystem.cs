using Content.Shared.Storage.EntitySystems;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaRewardItemSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<BossArenaRewardItemComponent, MapInitEvent>(OnInit);

    private void OnInit(EntityUid uid, BossArenaRewardItemComponent reward, MapInitEvent args)
    {
        if (Transform(uid).MapUid is not { } map || !TryComp<BossArenaMapComponent>(map, out var arena)) return;
        var prototype = EntityManager.System<BossArenaContentSystem>().Resource(arena.ArenaId, reward.Role) ?? reward.Fallback; // use the fallback if no reward provider exists
        var item = Spawn(prototype, Transform(uid).Coordinates);
        if (!EntityManager.System<SharedEntityStorageSystem>().Insert(item, uid)) Del(item);
    }
}
