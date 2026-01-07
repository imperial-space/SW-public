using System.Linq;
using System.Numerics;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Trigger.Systems;

public sealed partial class VectoredSpawnOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VectoredSpawnOnTriggerComponent, TriggerEvent>(HandleSpawnOnTrigger);
    }

    private void HandleSpawnOnTrigger(Entity<VectoredSpawnOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        SpawnOnTrigger(ent, ent.Comp);
    }


    private void SpawnOnTrigger(EntityUid uid, VectoredSpawnOnTriggerComponent component)
    {
        var coords = _transform.GetMapCoordinates(uid);
        var length = component.SpawnPositions.Count;
        for (var i = 0; i < length; i++)
        {
            var resultCoords = new MapCoordinates(coords.Position + component.SpawnPositions[i], coords.MapId);
            var entityClone = Spawn(component.SpawnedEntityID, resultCoords);
        }
    }
}
