using System.Linq;
using Content.Server.Spawners.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.RandomPoolSpawner;

public sealed class RandomPoolSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<string> _processedGroups = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RandomPoolSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<RandomPoolSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.GroupId == string.Empty)
            return;

        if (!_processedGroups.Add(ent.Comp.GroupId))
            return;

        RunGroup(ent.Comp.GroupId);
    }

    private void RunGroup(string groupId)
    {
        var spawners = new List<(EntityUid uid, RandomPoolSpawnerComponent comp)>();

        var query = EntityManager.AllEntityQueryEnumerator<RandomPoolSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.GroupId == groupId && Exists(uid))
                spawners.Add((uid, comp));
        }

        if (spawners.Count == 0)
            return;

        var pool = spawners[0].comp.Pool.ToList();

        if (pool.Count == 0)
            return;

        pool = pool.OrderBy(_ => _random.Next()).ToList();

        var spots = spawners.Select(x => x.uid)
                            .OrderBy(_ => _random.Next())
                            .ToList();

        var mainCount = Math.Min(pool.Count, spots.Count);

        for (var i = 0; i < mainCount; i++)
            Spawn(pool[i], Transform(spots[i]).Coordinates);

        pool = pool.Skip(mainCount).ToList();
        spots = spots.Skip(mainCount).ToList();

        var extraChance = spawners[0].comp.ExtraChance;
        var extraAttempts = spawners[0].comp.ExtraAttempts;

        for (var i = 0; i < extraAttempts; i++)
        {
            if (!_random.Prob(extraChance))
                break;

            if (pool.Count == 0 || spots.Count == 0)
                break;

            var entIndex = _random.Next(pool.Count);
            var spotIndex = _random.Next(spots.Count);

            Spawn(pool[entIndex], Transform(spots[spotIndex]).Coordinates);

            extraChance -= 0.1f;
            pool.RemoveAt(entIndex);
            spots.RemoveAt(spotIndex);
        }

        foreach (var (uid, comp) in spawners)
        {
            if (comp.DeleteAfterSpawn && Exists(uid))
                QueueDel(uid);
        }
    }
}
