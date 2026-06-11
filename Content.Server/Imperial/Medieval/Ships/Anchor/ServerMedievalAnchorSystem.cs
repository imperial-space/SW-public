using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Timing;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Medieval.Ships.Anchor;

public sealed class ServerMedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalAnchorComponent, UseAnchorEvent>(OnUseAnchor);
    }

    private void OnStartup(EntityUid uid, MedievalAnchorComponent component, ComponentStartup args)
    {
        UpdateAnchorVisuals(uid, component);
    }

    private void OnUseAnchor(EntityUid uid, MedievalAnchorComponent component, UseAnchorEvent args)
    {
        if (args.Target == null || args.Cancelled)
            return;

        if (!_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            return;

        var anchorDown = component.Enabled;
        var anchorTransform = Transform(uid);
        var grid = anchorTransform.GridUid;

        ShuttleComponent? shuttleComponent = null;
        if (!grid.HasValue || !anchorTransform.Anchored || !Resolve(grid.Value, ref shuttleComponent) ||
            !TryComp<ShipDrowningComponent>(grid.Value, out var shipDrowningComponent))
            return;

        if (!anchorDown)
        {
            shuttleComponent.Enabled = false;

            if (TryComp<PhysicsComponent>(grid.Value, out var body))
            {
                // Keep the ship dynamic so sea waves and other ambient physics continue updating while anchored.
                _physics.SetBodyType(grid.Value, BodyType.Dynamic, body: body);
                _physics.SetBodyStatus(grid.Value, body, BodyStatus.InAir);
                _physics.SetFixedRotation(grid.Value, true, body: body); // Считаю нужно это убрать
                _physics.SetLinearVelocity(grid.Value, Vector2.Zero, body: body);
                _physics.SetAngularVelocity(grid.Value, 0f, body: body);
            }

            if (SearchIslandInRange(uid, component.IslandSearchRange))
                shipDrowningComponent.AnchorUsedTime = _timing.CurTime;
            else
                shipDrowningComponent.AnchorUsedTime = null;
        }
        else
        {
            shuttleComponent.Enabled = true;
            _shuttleSystem.Enable(grid.Value);

            shipDrowningComponent.AnchorUsedTime = null;
        }

        component.Enabled = !anchorDown;
        UpdateAnchorVisuals(uid, component);
        args.Handled = true;
    }

    private void UpdateAnchorVisuals(EntityUid uid, MedievalAnchorComponent component)
    {
        _appearance.SetData(uid, MedievalAnchorVisuals.Enabled, component.Enabled);
    }

    private bool SearchIslandInRange(EntityUid uid, float range)
    {
        var searchBox = Box2.CenteredAround(_transform.GetWorldPosition(uid), new Vector2(range, range));

        var mapManager = IoCManager.Resolve<IMapManager>();

        var worldPos = _transform.GetWorldPosition(uid);
        var gridRange = new Vector2(range, range);

        List<Entity<MapGridComponent>> grids = [];
        mapManager.FindGridsIntersecting(Transform(uid).MapID, new Box2(worldPos - gridRange, worldPos + gridRange), ref grids);

        foreach (var grid in grids)
        {
            if (HasComp<IslandComponent>(grid))
                return true;
        }

        return false;
    }
}
