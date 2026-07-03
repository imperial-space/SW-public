using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializePlayerSpawn()
    {
        SubscribeLocalEvent<MedievalBeePlayerSpawnComponent, MapInitEvent>(SpawnInitialize);
        SubscribeLocalEvent<MedievalBeePlayerSpawnComponent, InteractHandEvent>(ExitInteract);
    }
    private void SpawnInitialize(Entity<MedievalBeePlayerSpawnComponent> spawn, ref MapInitEvent args)
    {
        if (!TryGetHiveGridFromTransform(spawn, out var grid))
            return;

        if (!grid.Value.Comp.Hive.HasValue)
        {
            Log.Warning("player spawn spawned on invalid grid, despawning");
            QueueDel(spawn);
            return;
        }

        grid.Value.Comp.Spawns.Add(spawn);
        spawn.Comp.Hive = grid.Value.Comp.Hive.Value;
    }
    private void ExitInteract(EntityUid uid, MedievalBeePlayerSpawnComponent component, InteractHandEvent args)
    {
        Teleport(args.User, component.Hive);
    }
}
