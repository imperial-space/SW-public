using System;
using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.Oar;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Medieval.Ships.Oar;

public sealed class OarSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;

    private const float MinWeight = 10f;

    public override void Initialize()
    {
        SubscribeLocalEvent<OarComponent, OnOarDoAfterEvent>(OnOarDoAfter);
    }

    private void OnOarDoAfter(EntityUid uid, OarComponent component, ref OnOarDoAfterEvent args)
    {
        var item = _hands.GetActiveItem(args.User);
        if (args.Cancelled || args.Handled || item == null)
            return;

        if (!_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            return;

        if (!TryComp<OarComponent>(item, out var oarComp))
            return;

        Push(oarComp.Direction, oarComp.Power, args.User);
        args.Handled = true;
        args.Repeat = true;
    }

    private void Push(Angle direction, float power, EntityUid player)
    {
        power += power * (_skills.GetSkillLevel(player, "Strength") - 10) * 0.1f;

        var boat = _transform.GetParentUid(player);
        if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
            return;

        var weight = MathF.Max(MinWeight, _rdWeight.GetTotal(boat));

        var normalizedAngle = (float) direction.Theta % (2 * MathF.PI);
        if (normalizedAngle < 0)
            normalizedAngle += 2 * MathF.PI;

        var directionVec = new Vector2(MathF.Cos(normalizedAngle), MathF.Sin(normalizedAngle));
        var impulse = directionVec * (power / weight);
        if (!TryComp<PhysicsComponent>(boat, out var body))
            return;

        _physics.WakeBody(boat);
        _physics.ApplyLinearImpulse(boat, impulse, body: body);
    }
}
