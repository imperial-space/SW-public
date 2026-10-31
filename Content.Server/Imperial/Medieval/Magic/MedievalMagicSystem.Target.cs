using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Imperial.Medieval.Magic.SpellTypes;
using Content.Shared.Imperial.MouseInput.Events;
using Robust.Shared.Map;

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

        foreach (var spawnedDirection in args.ProjectilePrototype.Keys)
        {
            var projectilePrototype = args.ProjectilePrototype[spawnedDirection];
            var evBefore = new MedievalBeforeSpawnEntityBySpellEvent()
            {
                Action = action,
                Performer = performer,
                SpawnedEntityPrototype = projectilePrototype,
                Direction = spawnedDirection,
                Coordinates = xform.Coordinates
            };

            RaiseLocalEvent(action, ref evBefore);
            if (evBefore.Cancelled || !CanSpawnArenaSpell(projectilePrototype, xform.Coordinates, performer, action)) continue;

            var ent = SpawnArenaSpell(projectilePrototype, xform.Coordinates, performer);
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
        if (evBefore.Cancelled || !CanSpawnArenaSpell(evBefore.SpawnedEntityPrototype, relativeCoords, performer, action)) return;

        var ent = SpawnArenaSpell(evBefore.SpawnedEntityPrototype, relativeCoords, performer);
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
