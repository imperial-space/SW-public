using System.Linq;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Bee.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Bee.Systems;

public sealed partial class MedievalBeeSystem : EntitySystem
{
    private void InitializeHive()
    {
        SubscribeLocalEvent<MedievalBeeHiveComponent, ComponentInit>(HiveInitialize);
        SubscribeLocalEvent<MedievalBeeHiveComponent, InteractHandEvent>(HiveInteract);
        SubscribeLocalEvent<MedievalBeeHiveComponent, ExaminedEvent>(HiveExamined);
        SubscribeLocalEvent<MedievalBeeHiveComponent, DestructionEventArgs>(HiveDestroyed);
    }
    private void HiveInitialize(EntityUid uid, MedievalBeeHiveComponent component, ComponentInit args)
    {
        if (!_mapLoader.TryLoadMap(new(_random.Pick(_proto.Index(component.GridDataset).Values)), out var createdMap, out var grids))
        {
            Log.Error("failure while initializing bee hive");
            return;
        }
        if (grids.Count > 1)
        {
            Log.Warning("loading bee hive with multiple grids, may cause issues");
        }
        var grid = grids.First();
        var gridComponent = EnsureComp<MedievalBeeGridComponent>(grid);
        component.Grid = (grid, gridComponent);
        gridComponent.Hive = uid;
        _mapSystem.InitializeMap((createdMap.Value.Owner, createdMap.Value.Comp));
    }
    private void HiveInteract(EntityUid uid, MedievalBeeHiveComponent component, InteractHandEvent args)
    {
        if (component.Grid.Comp.Spawns.Count() <= 0)
        {
            return;
        }
        var spawn = _random.Pick(component.Grid.Comp.Spawns);
        Teleport(args.User, spawn.Owner);
        args.Handled = true;
    }
    private void HiveExamined(EntityUid uid, MedievalBeeHiveComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"medieval-bee-pacified-{component.Pacified.ToString().ToLower()}"));
        if (component.PacifyEnd.HasValue)
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-yes", ("time", (component.PacifyEnd.Value - _timing.CurTime).ToString())));
        }
        else
        {
            args.PushMarkup(Loc.GetString("medieval-bee-smoke-no"));
        }
    }
    private void HiveDestroyed(EntityUid uid, MedievalBeeHiveComponent component, DestructionEventArgs args)
    {
        HashSet<Entity<MobStateComponent>> mobs = new();
        _lookup.GetGridEntities(component.Grid, mobs);
        foreach (var mob in mobs)
        {
            Teleport(mob.Owner, uid);
        }
        QueueDel(Transform(component.Grid).MapUid);
    }
    private void UpdateHive(float frameTime)
    {
        var hiveQuery = EntityQueryEnumerator<MedievalBeeHiveComponent>();
        while (hiveQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.PacifyEnd > _timing.CurTime)
                continue;

            comp.PacifyEnd = null;
            if (comp.Pacified)
                UnPacify((uid, comp));

        }
    }
}
