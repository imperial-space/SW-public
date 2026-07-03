using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeChanceSpawn()
    {
        SubscribeLocalEvent<MedievalBeeChanceSpawnComponent, MapInitEvent>(ChanceSpawnInit);
    }
    private void ChanceSpawnInit(EntityUid uid, MedievalBeeChanceSpawnComponent component, MapInitEvent args)
    {
        if (!_random.Prob(component.Chance))
            return;

        if (!_random.TryPickAndTake(component.Entities, out var ent))
            return;

        Spawn(ent, Transform(uid).Coordinates);
    }
}
