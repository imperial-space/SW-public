using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Boss;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed class BossArenaKeySystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        EntityManager.System<BossFeatureSystem>().Register("arena-key");
        SubscribeLocalEvent<BossArenaAccessEvent>(OnAccess);
    }

    private void OnAccess(ref BossArenaAccessEvent args)
    {
        args.Required = true;                                                    // installing this rule makes a key required
        args.Allowed = args.Item is { } item && Exists(item) &&
            TryComp<BossArenaGateKeyComponent>(item, out var key) &&
            (key.ArenaId.Length == 0 || key.ArenaId == args.ArenaId) && _hands.GetActiveItem(args.User) == item;
        args.ConsumeItem = args.Allowed;
    }
}
