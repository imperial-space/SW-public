using System.Numerics;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;


using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Oar;


namespace Content.Server.Imperial.Medieval.Ships.Oar;

/// <summary>
/// This handles...
/// </summary>
public sealed class OarSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem  _skills = default!;
    [Dependency] private readonly EntityManager _entManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem  _rdWeight = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, AfterInteractEvent>(OnOarAfterInteract);
        SubscribeLocalEvent<OarComponent, OnOarDoAfterEvent>(OnOarDoAfter);
    }

    private void OnOarAfterInteract(EntityUid uid, OarComponent component, AfterInteractEvent args)
    {
        var playerEntity = args.User;

        if (args.Handled || !args.CanReach )
            return;

        var boat = _transform.GetParentUid(playerEntity);

        if (boat == args.ClickLocation.EntityId)
            return;

        var clickEntity = args.ClickLocation.EntityId;
        if (boat == _transform.GetParentUid(clickEntity))
            return;

        var time = 7 -_skills.GetSkillLevel(playerEntity, "Agility") * 0.3f;
        var sdoAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new OnOarDoAfterEvent(),
            args.Used,
            args.Target,
            args.Used)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(sdoAfter);
        var playerPosition = _transform.GetWorldPosition(playerEntity);
        var boatPosition = _transform.ToWorldPosition(args.ClickLocation);
        var direction = (playerPosition - boatPosition).Normalized();
        component.Direction = direction;
    }

    private void OnOarDoAfter(EntityUid uid, OarComponent component, ref OnOarDoAfterEvent args)
    {
        var item = _hands.GetActiveItem(args.User);
        if (args.Cancelled || args.Handled || item == null)
            return;

        if (!TryComp<OarComponent>(item, out var comp))
            return;

        Push(item.Value, comp.Direction, comp.Power, args.User);
        args.Handled = true;
        args.Repeat = true;
    }

    private void Push(EntityUid item, Vector2 direction, float power, EntityUid player)
    {
        power += power * (10 - _skills.GetSkillLevel(player, "Strength")) * 0.1f;

        var boat = _transform.GetParentUid(player);

        var entities = _lookup.GetEntitiesIntersecting(boat);

        if (entities.Count > 1000)
            return;

        var weight = _rdWeight.GetTotal(boat);

        foreach (var entity in entities)
        {
            if (HasComp<RDWeightComponent>(entity))
                weight += _rdWeight.GetTotal(entity);
        }

        if (weight == 0)
            weight = 10;
        var impulse = direction * (power / weight);
        var angleimpulse = (power / weight);
        if (direction.X < 0)
            angleimpulse = -angleimpulse;

        if (EntityManager.TryGetComponent(boat, out PhysicsComponent? body))
        {
            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, impulse, body: body);
            _physics.ApplyAngularImpulse(boat, angleimpulse, body: body);
        }
    }
}
