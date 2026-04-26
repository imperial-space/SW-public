using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Medieval.Ships.Anchor;

public sealed class ServerMedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, UseAnchorEvent>(OnUseAnchor);
    }

    private void OnUseAnchor(EntityUid uid, MedievalAnchorComponent component, UseAnchorEvent args)
    {
        if (args.Target == null || args.Cancelled)
            return;

        if (!_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            return;

        var anchor = component.Owner;
        var anchorDown = component.Enabled;
        var anchorTransform = Transform(anchor);
        var grid = anchorTransform.GridUid;

        ShuttleComponent? shuttleComponent = null;
        if (!grid.HasValue || !anchorTransform.Anchored || !Resolve(grid.Value, ref shuttleComponent))
            return;

        if (!anchorDown)
        {
            shuttleComponent.Enabled = false;

            if (TryComp<PhysicsComponent>(grid.Value, out var body))
            {
                // Keep the ship dynamic so sea waves and other ambient physics continue updating while anchored.
                _physics.SetBodyType(grid.Value, BodyType.Dynamic, body: body);
                _physics.SetBodyStatus(grid.Value, body, BodyStatus.InAir);
                _physics.SetFixedRotation(grid.Value, true, body: body);
                _physics.SetLinearVelocity(grid.Value, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(grid.Value, 0f, body: body);
            }
        }
        else
        {
            shuttleComponent.Enabled = true;
            _shuttleSystem.Enable(grid.Value);
        }

        var nextAnchorPrototype = anchorDown ? "MedievalAnchorUp" : "MedievalAnchorDown";
        Spawn(nextAnchorPrototype, anchorTransform.Coordinates);
        Del(anchor);
    }
}
