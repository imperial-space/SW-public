using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Random;
using System.ComponentModel;

namespace Content.Shared.Trigger.Components.Effects;

public sealed partial class RandomOffsetSpawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomOffsetSpawnComponent, TriggerEvent>(HandleSpawnOnTrigger);
    }

    private void HandleSpawnOnTrigger(Entity<RandomOffsetSpawnComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        SpawnOnTrigger(ent, ent.Comp);
    }

    private void SpawnOnTrigger(EntityUid uid, RandomOffsetSpawnComponent component)
    {
        var coords = _transform.GetMapCoordinates(uid);
        Vector2 randomVector;
        Vector2 randomDirectionFromAngle;
        MapCoordinates resultCoords;
        bool needNewCoords;
        for (var i = 0; i < component.Quantity; i++)
        {
            if (component.NeedUniqueСoords)
                do
                {
                    needNewCoords = false;
                    randomVector = _random.NextVector2() * _random.Next(component.Radius + 1);
                    resultCoords = new MapCoordinates(coords.Position + randomVector, coords.MapId);
                    foreach (var entityNear in _lookup.GetEntitiesInRange(resultCoords, 0.1f))
                        if (component.SpawnedEntitiesUID.Contains(entityNear))
                            needNewCoords = true;
                } while (needNewCoords);
            else
            {
                randomVector = new Vector2(_random.Next(-component.Radius, component.Radius+1), _random.Next(-component.Radius, component.Radius+1));
                resultCoords = new MapCoordinates(coords.Position + randomVector, coords.MapId);
            }
            var entityClone = Spawn(component.SpawnedEntityID, resultCoords);
            component.SpawnedEntitiesUID.Add(entityClone);
        }
    }
}

