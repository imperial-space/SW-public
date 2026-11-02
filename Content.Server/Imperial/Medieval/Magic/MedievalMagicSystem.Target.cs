using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Imperial.Medieval.Magic.Overlays;
using Content.Shared.Imperial.Medieval.Magic.SpellTypes;
using Content.Shared.Imperial.MouseInput.Events;
using Robust.Shared.Map;
using Content.Shared.Projectiles;

namespace Content.Server.Imperial.Medieval.Magic;


/// <summary>
/// The server part is responsible for casting target spells
/// </summary>
public sealed partial class MedievalMagicSystem
{
    private void InitializeTargetSpells()
    {
        SubscribeLocalEvent<MedievalTargetSpellComponent, MedievalSpellRelayEvent<MousePositionRefreshEvent>>(OnMousePositionRefresh);
        SubscribeNetworkEvent<MedievalProjectileTargetEvent>(OnPredictedProjectileTarget);
    }

    #region Event Handlers

    private void OnPredictedProjectileTarget(MedievalProjectileTargetEvent args, EntitySessionEventArgs session)
    {
        if (args.Coordinates is not { } coordinates || session.SenderSession.AttachedEntity is not { } performer ||
            !TryGetEntity(args.Action, out var action) || action == null ||
            !TryComp<MedievalTargetSpellComponent>(action.Value, out var targetSpell) ||
            targetSpell.SpellData is not MedievalProjectileSpellData data ||
            data.InputTick != args.InputTick || GetEntity(data.Performer) != performer)
            return;
        var target = GetCoordinates(coordinates);
        if (!target.IsValid(EntityManager) || _transformSystem.GetMapId(target) != Transform(performer).MapID)
            return;
        targetSpell.SpellData = null;
        data.Coordinates = coordinates;
        data.TargetSpellType = TargetSpellType.TargetOnLastMousePosition;
        CastProjectileSpell(data);
    }

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

        var predictionIndex = 0;
        var launched = false;
        foreach (var spawnedDirection in args.ProjectilePrototype.Keys)
        {
            var projectilePrototype = args.ProjectilePrototype[spawnedDirection];
            var key = new ProjectilePredictionKey(args.Action, args.InputTick, predictionIndex++);
            var evBefore = new MedievalBeforeSpawnEntityBySpellEvent()
            {
                Action = action,
                Performer = performer,
                SpawnedEntityPrototype = projectilePrototype,
                Direction = spawnedDirection,
                Coordinates = xform.Coordinates
            };

            RaiseLocalEvent(action, ref evBefore);
            if (evBefore.Cancelled) continue;

            var ent = Spawn(projectilePrototype, xform.Coordinates);
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

            _spellProjectiles.MarkPrediction(ent, performer, key);
            launched = true;
            _gunSystem.ShootProjectile(ent, direction, userVelocity, performer, performer, speed: args.ProjectileSpeed);
        }

        _spellProjectiles.CompletePrediction(args.Action, args.InputTick, performer, launched);
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
        // use the same completion-time cursor sample as the local preview.
        if (args is MedievalProjectileSpellData && args.InputTick != Robust.Shared.Timing.GameTick.Zero)
            RaiseNetworkEvent(new MedievalProjectileTargetEvent { Action = args.Action, InputTick = args.InputTick }, performer);
        else
            _mouseInputSystem.CreateMousePositionRequest(performer);

        return false;
    }

    #endregion
}
