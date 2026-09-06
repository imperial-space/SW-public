using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.AnimalHusbandry;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.Imperial.Medieval.Animals;

/// <summary>
/// System that manages the production of excrements by entities.
/// </summary>
public sealed partial class ExcrementProducerSystem: EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ExcrementProducerComponent>();
        while (query.MoveNext(out var uid, out var excrementProducer))
        {
            if (_mobState.IsDead(uid))
                continue;

            // Disallow infinitely pooping by checking if the entity has enough hunger to produce excrement.
            if (TryComp<HungerComponent>(uid, out var hunger))
            {
                if (_hunger.GetHungerThreshold(hunger) <= HungerThreshold.Starving)
                    continue;
            }

            if (_timing.CurTime > excrementProducer.LastProducedTime + excrementProducer.ProduceMinimumInterval)
            {
                TryExcrete(uid, excrementProducer);
            }
        }
    }

    private void TryExcrete(EntityUid uid, ExcrementProducerComponent excrementProducer)
    {
        if (_random.Prob(excrementProducer.ProduceChance))
        {
            excrementProducer.LastProducedTime = _timing.CurTime;
            _adminLog.Add(LogType.Action, $"{ToPrettyString(uid)} excreted.");


            foreach (var ent in EntitySpawnCollection.GetSpawns(excrementProducer.ExcrementSpawn, _random))
            {
                Spawn(ent, Transform(uid).Coordinates);
            }
        }
    }
}
