using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Imperial.Medieval.Magic.SpellTypes;
using Content.Shared.Imperial.MouseInput.Events;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// The server part is responsible for casting target spells
/// </summary>
public sealed partial class MedievalMagicSystem
{
    private void InitializeTargetSpells()
    {
        SubscribeLocalEvent<MedievalTargetSpellComponent, MedievalSpellRelayEvent<MousePositionRefreshEvent>>(OnMousePositionRefresh);
    }

    #region Event Handlers

    private void OnMousePositionRefresh(EntityUid uid, MedievalTargetSpellComponent component, MedievalSpellRelayEvent<MousePositionRefreshEvent> args)
    {
        var data = component.SpellData;

        if (data == null) return;

        data.Coordinates = GetNetCoordinates(args.Args.MousePosition);
        data.TargetSpellType = TargetSpellType.TargetOnLastMousePosition;

        CastSpell(data);

        component.SpellData = null;
    }

    #endregion

    // Keeps the projectile from appearing inside a wall right next to the caster
    private const float ProjectileWallGap = 0.15f;

    #region Spell Cast

    protected override void CastProjectileSpell(MedievalProjectileSpellData args)
    {
        if (!CanInstantlyCastSpell(args, args.TargetSpellType)) return;

        var performer = GetEntity(args.Performer);
        var action = GetEntity(args.Action);

        var xform = Transform(performer);

        var spawnMapCoords = _transformSystem.ToMapCoordinates(xform.Coordinates);
        var targetMapCoords = _transformSystem.ToMapCoordinates(args.Coordinates);

        var userVelocity = _physicsSystem.GetMapLinearVelocity(performer);

        var spawnCoords = xform.Coordinates;
        var toTarget = targetMapCoords.Position - spawnMapCoords.Position;

        if (args.SpawnForwardOffset > 0f && toTarget.LengthSquared() > 0.01f)
        {
            var forward = toTarget.Normalized();
            var offset = Math.Min(args.SpawnForwardOffset, toTarget.Length());
            var ray = new CollisionRay(spawnMapCoords.Position, forward, (int) CollisionGroup.Impassable);

            foreach (var hit in _physicsSystem.IntersectRay(spawnMapCoords.MapId, ray, offset + ProjectileWallGap, performer, returnOnFirstHit: true))
            {
                offset = Math.Max(0f, hit.Distance - ProjectileWallGap);
                break;
            }

            spawnCoords = _transformSystem.ToCoordinates(
                new MapCoordinates(spawnMapCoords.Position + forward * offset, spawnMapCoords.MapId));
        }

        foreach (var spawnedDirection in args.ProjectilePrototype.Keys)
        {
            var projectilePrototype = args.ProjectilePrototype[spawnedDirection];
            var evBefore = new MedievalBeforeSpawnEntityBySpellEvent()
            {
                Action = action,
                Performer = performer,
                SpawnedEntityPrototype = projectilePrototype,
                Direction = spawnedDirection,
                Coordinates = spawnCoords
            };

            RaiseLocalEvent(action, ref evBefore);
            if (evBefore.Cancelled) continue;

            var ent = Spawn(projectilePrototype, spawnCoords);

            if (TryComp<MedievalDetonateAtTargetComponent>(ent, out var detonate))
                detonate.Target = targetMapCoords;

            var direction = spawnedDirection.RotateVec(targetMapCoords.Position - spawnMapCoords.Position);
            var rotation = spawnedDirection;

            if (TryComp<ShowSpawnedEntityComponent>(action, out var showSpawnedEntityComponent))
                rotation = showSpawnedEntityComponent.Rotation;

            var ev = new MedievalAfterSpawnEntityBySpellEvent()
            {
                Action = action,
                Performer = performer,
                Rotation = rotation,
                SpawnedEntity = ent
            };

            RaiseLocalEvent(ent, ev);
            RaiseLocalEvent(action, ev);

            _gunSystem.ShootProjectile(ent, direction, userVelocity, performer, performer, speed: args.ProjectileSpeed);
        }

        base.CastProjectileSpell(args);
    }


    protected override void CastSpawnSpell(MedievalSpawnSpellData args)
    {
        if (!CanInstantlyCastSpell(args, args.TargetSpellType)) return;

        var performer = GetEntity(args.Performer);
        var action = GetEntity(args.Action);

        var xform = Transform(performer);

        var spawnMapCoords = _transformSystem.ToMapCoordinates(xform.Coordinates);
        var targetMapCoords = args.SpawnSpellType == SpawnSpellType.SpawnOnMousePosition ? _transformSystem.ToMapCoordinates(args.Coordinates) : spawnMapCoords;
        var relativeCoords = new MapCoordinates(targetMapCoords.Position + args.RelativePosition, targetMapCoords.MapId);

        var evBefore = new MedievalBeforeSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            SpawnedEntityPrototype = args.SpawnedEntityPrototype,
            Coordinates = _transformSystem.ToCoordinates(relativeCoords)
        };

        RaiseLocalEvent(action, ref evBefore);
        if (evBefore.Cancelled) return;

        var ent = Spawn(evBefore.SpawnedEntityPrototype, relativeCoords);
        var rotation = Angle.FromDegrees(0);

        if (TryComp<ShowSpawnedEntityComponent>(action, out var showSpawnedEntityComponent))
            rotation = showSpawnedEntityComponent.Rotation;

        var ev = new MedievalAfterSpawnEntityBySpellEvent()
        {
            Action = action,
            Performer = performer,
            Rotation = rotation,
            SpawnedEntity = ent
        };

        RaiseLocalEvent(ent, ev);
        RaiseLocalEvent(action, ev);

        base.CastSpawnSpell(args);
    }

    protected override void CastTeleportSpell(MedievalTeleportSpellData args)
    {
        base.CastTeleportSpell(args);
    }

    #endregion

    #region Helpers

    protected override bool CanInstantlyCastSpell(MedievalTargetSpellData args, TargetSpellType spellType)
    {
        var performer = GetEntity(args.Performer);
        var action = GetEntity(args.Action);

        if (spellType != TargetSpellType.TargetOnCurrentMousePosition) return true;
        if (!TryComp<MedievalTargetSpellComponent>(action, out var targetSpellComponent)) return true;

        targetSpellComponent.SpellData = args;
        _mouseInputSystem.CreateMousePositionRequest(performer);

        return false;
    }

    #endregion
}
