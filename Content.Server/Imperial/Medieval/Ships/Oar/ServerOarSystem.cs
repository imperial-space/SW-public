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
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;


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
        SubscribeLocalEvent<OarComponent, OnOarDoAfterEvent>(OnOarDoAfter);
    }

    private void OnOarDoAfter(EntityUid uid, OarComponent component, ref OnOarDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            RemComp<NoRotateOnInteractComponent>(args.User);
            RemComp<NoRotateOnMoveComponent>(args.User);
        }

        var item = _hands.GetActiveItem(args.User);
        if (args.Cancelled || args.Handled || item == null)
            return;

        if (!TryComp<OarComponent>(item, out var comp))
            return;

        Push(item.Value, comp.Direction, comp.Power, args.User);
        args.Handled = true;
        args.Repeat = true;
    }

    private void Push(EntityUid item, Angle direction, float power, EntityUid player)
    {
        power += power * (-10 + _skills.GetSkillLevel(player, "Strength")) * 0.1f;

        var boat = _transform.GetParentUid(player);

        var boatAngle = _transform.GetWorldRotation(boat);

        var playerAngle = _transform.GetWorldRotation(player);

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

        var directionVec = Vector2.Zero;
        while (direction > 2)
        {
            direction -= 2;
        }


        direction += Angle.FromDegrees(45);
        // от 0 до 0.5 (1,0)
        // от 0.5 до 1 (0,1)
        // от 1 до 1,5 (0,-1)
        // от 1,5 до 0 (-1,0)
        if (direction > 1)
        {
            if (direction > 1.5)
                directionVec = new Vector2(-1,0);
            else
            {
                directionVec = new Vector2(0,-1);
            }
        }
        else
        {
            if (direction > 0.5)
                directionVec = new Vector2(0,1);
            else
            {
                directionVec = new Vector2(1,0);
            }
        }

        directionVec = playerAngle.RotateVec(directionVec);
        var impulse = directionVec * (power / weight);
        var angleimpulse = (power / weight);

        if (EntityManager.TryGetComponent(boat, out PhysicsComponent? body))
        {
            _physics.WakeBody(boat);
            _physics.ApplyLinearImpulse(boat, impulse, body: body);
            // _physics.ApplyAngularImpulse(boat, angleimpulse, body: body);
        }
    }
}
