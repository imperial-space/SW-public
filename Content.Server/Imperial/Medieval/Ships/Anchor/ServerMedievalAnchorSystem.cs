using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Anchor;

public sealed class ServerMedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MedievalAnchorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MedievalAnchorComponent, ToggleAnchorEvent>(OnToggleAnchor);
        SubscribeLocalEvent<MedievalAnchorComponent, ExaminedEvent>(OnExamine);
    }

    private void OnStartup(EntityUid uid, MedievalAnchorComponent component, ComponentStartup args)
    {
        UpdateVisuals(uid, component);
    }

    private void OnMapInit(EntityUid uid, MedievalAnchorComponent component, MapInitEvent args)
    {
        if (!TryGetShip(uid, out var ship, out var shuttle, out var body))
            return;

        var drowning = EnsureComp<ShipDrowningComponent>(ship);
        SetWaveProtection(uid, component, drowning, component.Lowered);

        if (component.Lowered)
            StopShip(ship, shuttle, body);
    }

    private void OnToggleAnchor(EntityUid uid, MedievalAnchorComponent component, ToggleAnchorEvent args)
    {
        try
        {
            if (args.Cancelled ||
                args.Handled ||
                args.Target != uid ||
                component.ActiveUser != args.User ||
                !_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
            {
                return;
            }

            if (!TryGetShip(uid, out var ship, out var shuttle, out var body))
                return;

            var drowning = EnsureComp<ShipDrowningComponent>(ship);
            SetLowered(uid, component, ship, shuttle, body, drowning, !component.Lowered);
            _audio.PlayPvs(MedievalShipSounds.AnchorUse, uid);
            args.Handled = true;
        }
        finally
        {
            ClearActiveUser(uid, component);
        }
    }

    private void SetLowered(
        EntityUid anchor,
        MedievalAnchorComponent component,
        EntityUid ship,
        ShuttleComponent shuttle,
        PhysicsComponent body,
        ShipDrowningComponent drowning,
        bool lowered)
    {
        if (lowered)
            StopShip(ship, shuttle, body);
        else
            StartShip(ship, shuttle, body);

        component.Lowered = lowered;
        SetWaveProtection(anchor, component, drowning, lowered);
        UpdateVisuals(anchor, component);
    }

    private void StopShip(EntityUid ship, ShuttleComponent shuttle, PhysicsComponent body)
    {
        shuttle.Enabled = false;
        _physics.SetBodyType(ship, BodyType.Dynamic, body: body);
        _physics.SetBodyStatus(ship, body, BodyStatus.InAir);
        _physics.SetFixedRotation(ship, true, body: body);
        _physics.SetLinearVelocity(ship, Vector2.Zero, body: body);
        _physics.SetAngularVelocity(ship, 0f, body: body);
    }

    private void StartShip(EntityUid ship, ShuttleComponent shuttle, PhysicsComponent body)
    {
        shuttle.Enabled = true;
        _shuttle.Enable(ship, component: body, shuttle: shuttle);
    }

    private void SetWaveProtection(
        EntityUid anchor,
        MedievalAnchorComponent component,
        ShipDrowningComponent drowning,
        bool lowered)
    {
        TimeSpan? disabledAt = null;
        if (lowered && IsIslandInRange(anchor, component.IslandSearchRange))
            disabledAt = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(0f, component.WaveDisableDelay));

        component.WavesDisabledAt = disabledAt;
        drowning.WavesDisabledAt = disabledAt;
    }

    private bool TryGetShip(
        EntityUid anchor,
        out EntityUid ship,
        out ShuttleComponent shuttle,
        out PhysicsComponent body)
    {
        var transform = Transform(anchor);
        if (transform.Anchored &&
            transform.GridUid is { } grid &&
            HasComp<MapGridComponent>(grid) &&
            TryComp<ShuttleComponent>(grid, out var foundShuttle) &&
            TryComp<PhysicsComponent>(grid, out var foundBody))
        {
            ship = grid;
            shuttle = foundShuttle;
            body = foundBody;
            return true;
        }

        ship = EntityUid.Invalid;
        shuttle = default!;
        body = default!;
        return false;
    }

    private bool IsIslandInRange(EntityUid anchor, float range)
    {
        if (range <= 0f)
            return false;

        var transform = Transform(anchor);
        var searchArea = new PhysShapeCircle(range, _transform.GetWorldPosition(transform));
        var grids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(
            transform.MapID,
            searchArea,
            Robust.Shared.Physics.Transform.Empty,
            ref grids,
            approx: false,
            includeMap: false);

        foreach (var grid in grids)
        {
            if (HasComp<IslandComponent>(grid))
                return true;
        }

        return false;
    }

    private void OnExamine(EntityUid uid, MedievalAnchorComponent component, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "examine-anchor-island-search-range",
            ("range", component.IslandSearchRange)));

        if (!component.Lowered)
        {
            var message = IsIslandInRange(uid, component.IslandSearchRange)
                ? "examine-anchor-island-near"
                : "examine-anchor-island-far";
            args.PushMarkup(Loc.GetString(message));
            return;
        }

        if (component.WavesDisabledAt is not { } disabledAt)
        {
            args.PushMarkup(Loc.GetString("examine-anchor-waves-will-not-disable"));
            return;
        }

        var remaining = disabledAt - _timing.CurTime;
        if (remaining > TimeSpan.Zero)
        {
            args.PushMarkup(Loc.GetString(
                "examine-anchor-time-to-disable-waves",
                ("seconds", (int) Math.Ceiling(remaining.TotalSeconds))));
            return;
        }

        args.PushMarkup(Loc.GetString("examine-anchor-waves-disabled"));
    }

    private void UpdateVisuals(EntityUid uid, MedievalAnchorComponent component)
    {
        _appearance.SetData(uid, MedievalAnchorVisuals.Lowered, component.Lowered);
    }

    private void ClearActiveUser(EntityUid uid, MedievalAnchorComponent component)
    {
        if (component.ActiveUser == null)
            return;

        component.ActiveUser = null;
        Dirty(uid, component);
    }
}
