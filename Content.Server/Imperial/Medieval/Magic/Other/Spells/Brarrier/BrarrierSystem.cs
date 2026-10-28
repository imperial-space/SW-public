using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// Monitors and implements barrier spawning
/// </summary>
public sealed partial class BrarrierSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrarrierComponent, MedievalAfterSpawnEntityBySpellEvent>(OnSpawn);
        SubscribeLocalEvent<CheckInitialBarrierComponent, MedievalBeforeSpawnEntityBySpellEvent>(OnBeforeSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<BrarrierComponent, MetaDataComponent>();

        while (enumerator.MoveNext(out var uid, out var component, out var metaDataComponent))
        {
            var barrierBounds = _physicsSystem.GetWorldAABB(uid);

            foreach (var clone in component.BarrierStack.ToList())
            {
                if (clone.Item1 > _timing.CurTime) continue;

                component.BarrierStack.Remove(clone);

                if (metaDataComponent.EntityPrototype == null) continue;
                if (!CanSpawn(component, barrierBounds, clone.Item2)) continue;

                var rules = EntityManager.System<Content.Shared.Imperial.Medieval.Boss.SharedBossArenaRulesSystem>();
                if (!rules.CanSpawn(component.SpawnedEntity, clone.Item2))
                    continue;
                using var sourceScope = rules.WithSource(uid);
                var spawned = Spawn(component.SpawnedEntity, clone.Item2);
                rules.TagSpell(spawned, uid);
            }
        }
    }

    private void OnSpawn(EntityUid uid, BrarrierComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        var startPosition = _transformSystem.GetMapCoordinates(uid);

        for (var i = 1; i < component.BarrierLength + 1; i++)
        {
            var rotationVector = component.OnlyOneSide ? (args.Rotation + Angle.FromDegrees(90)).ToVec() : args.Rotation.ToVec();
            var relativePosition = component.SpawnRelativePosition * rotationVector;

            component.BarrierStack.Add((
                _timing.CurTime + component.BarrierSpawnTime * i,
                new MapCoordinates(startPosition.Position + relativePosition * i, startPosition.MapId)
            ));

            if (component.OnlyOneSide) continue;

            component.BarrierStack.Add((
                _timing.CurTime + component.BarrierSpawnTime * i,
                new MapCoordinates(startPosition.Position - relativePosition * i, startPosition.MapId)
            ));
        }
    }
    private void OnBeforeSpawn(EntityUid uid, CheckInitialBarrierComponent component, ref MedievalBeforeSpawnEntityBySpellEvent args)
    {
        if (!_prototypeManager.Index(args.SpawnedEntityPrototype).TryGetComponent<BrarrierComponent>(out var brarrierComponent, _componentFactory)) return;
        if (!_prototypeManager.Index(args.SpawnedEntityPrototype).TryGetComponent<FixturesComponent>(out var fixturesComponent, _componentFactory)) return;

        var mapCoords = _transformSystem.ToMapCoordinates(args.Coordinates);
        var prespawmBounds = ComputeWorldAABWithoutEntity(mapCoords.Position, args.Rotation, fixturesComponent);

        if (CanSpawn(brarrierComponent, prespawmBounds, mapCoords)) return;

        args.Cancelled = brarrierComponent.SpawnedOnInvalidLocation == null;

        if (brarrierComponent.SpawnedOnInvalidLocation == null) return;

        args.SpawnedEntityPrototype = (EntProtoId)brarrierComponent.SpawnedOnInvalidLocation;
    }

    #region Helpers

    private bool CanSpawn(BrarrierComponent component, Box2 parentBounds, MapCoordinates coords)
    {
        var output = true;
        var intersects = _lookupSystem.GetEntitiesInRange(coords, component.LookupRadius, LookupFlags.Static);

        foreach (var ent in intersects)
        {
            if (!HasComp<PhysicsComponent>(ent)) continue;
            if (!HasComp<FixturesComponent>(ent)) continue;

            var (_, layer) = _physicsSystem.GetHardCollision(ent);
            var entBounds = _physicsSystem.GetWorldAABB(ent);

            var inBlackList = (component.LayersBlackList & layer) != 0;
            var intersectsPrecent = entBounds.IntersectPercentage(GetPrespawnAABB(parentBounds, coords.Position));

            if (!inBlackList) continue;
            if (intersectsPrecent <= component.PermissibleIntersectionsPercentage) continue;

            output = false;
        }

        return output;
    }

    private Box2 GetPrespawnAABB(Box2 parentAABB, Vector2 newPosition)
    {
        var ratio = parentAABB.Size / 2;

        return new Box2(
            newPosition - ratio,
            newPosition + ratio
        );
    }

    private Box2 ComputeWorldAABWithoutEntity(Vector2 worldPos, Angle worldRot, FixturesComponent fixturesComponent)
    {
        var transform = new Transform(worldPos, (float)worldRot.Theta);
        var bounds = new Box2(transform.Position, transform.Position);

        foreach (var fixture in fixturesComponent.Fixtures.Values)
        {
            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var boundy = fixture.Shape.ComputeAABB(transform, i);
                bounds = bounds.Union(boundy);
            }
        }

        return bounds;
    }

    #endregion
}
