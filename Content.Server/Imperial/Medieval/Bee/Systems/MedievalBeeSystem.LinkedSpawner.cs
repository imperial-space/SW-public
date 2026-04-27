using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Random.Helpers;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeLinkedSpawner()
    {
        SubscribeLocalEvent<MedievalBeeLinkedSpawnerComponent, MapInitEvent>(SpawnerInit);
        SubscribeLocalEvent<MedievalBeeLinkedMobComponent, MobStateChangedEvent>(LinkedMobStateChanged);
    }
    private void SpawnerInit(EntityUid uid, MedievalBeeLinkedSpawnerComponent component, MapInitEvent args)
    {
        component.NextSpawn = _timing.CurTime;
    }
    private void LinkedMobStateChanged(EntityUid uid, MedievalBeeLinkedMobComponent component, MobStateChangedEvent args)
    {
        if (!component.LinkedSpawner.HasValue)
            return;

        if (component.LinkedSpawner.Value.Comp.NextSpawn.HasValue)
            return;

        if (args.NewMobState == MobState.Alive)
            return;

        component.LinkedSpawner.Value.Comp.NextSpawn = _timing.CurTime + component.LinkedSpawner.Value.Comp.RespawnTime;
    }
    private void UpdateLinkedSpawner(float frameTime)
    {
        var spawnerQuery = EntityQueryEnumerator<MedievalBeeLinkedSpawnerComponent>();
        while (spawnerQuery.MoveNext(out var uid, out var comp))
        {
            if (!comp.NextSpawn.HasValue || comp.NextSpawn > _timing.CurTime)
                continue;

            if (comp.LinkedEntity.HasValue && !Deleted(comp.LinkedEntity) && (!TryComp<MobStateComponent>(comp.LinkedEntity.Value, out var state) || state.CurrentState == MobState.Alive))
                continue;

            var mob = _random.Pick(comp.Mobs);
            var createdMob = Spawn(mob, Transform(uid).Coordinates);
            QueueDel(comp.LinkedEntity);
            var mobComp = EnsureComp<MedievalBeeLinkedMobComponent>(createdMob);
            mobComp.LinkedSpawner = (uid, comp);
            comp.LinkedEntity = createdMob;
            comp.NextSpawn = null;
        }
    }
}
