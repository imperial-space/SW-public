using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval;
using Content.Shared.Imperial.Medieval.CartographerTable;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Medieval.CartographerTable;

public sealed class CartographerTableSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float _updateTimer = 0f;
    private const float UpdateInterval = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalCartographerTableComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer -= UpdateInterval;

        var query = EntityQueryEnumerator<MedievalCartographerTableComponent, RadarConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var tableComp, out var xform))
        {
            if (_uiSystem.GetActors(uid, RadarConsoleUiKey.Key).Count() == 0)
                continue;

            UpdateTableInterface(uid, tableComp, xform);
        }
    }

    private void OnUiOpened(EntityUid uid, MedievalCartographerTableComponent component, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(RadarConsoleUiKey.Key))
            return;

        if (!TryComp(uid, out RadarConsoleComponent? radarComp))
            return;

        UpdateTableInterface(uid, radarComp, Transform(uid));
    }

    private void UpdateTableInterface(EntityUid uid, RadarConsoleComponent radarComp, TransformComponent xform)
    {
        if (xform.MapID == MapId.Nullspace)
            return;

        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (radarComp.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (coordinates == null || angle == null)
            return;

        var netCoordinates = GetNetCoordinates(coordinates.Value);
        var rotateWithEntity = !radarComp.FollowEntity;
        var maxRange = radarComp.MaxRange;

        var markersToSend = new List<CartographerRadarMarkerData>();
        var markerQuery = EntityQueryEnumerator<CartographerRadarMarkerComponent, TransformComponent>();

        while (markerQuery.MoveNext(out var mUid, out var mComp, out var mXform))
        {
            if (mXform.MapID != xform.MapID || mXform.MapID == MapId.Nullspace)
                continue;

            var worldPos = _transform.GetWorldPosition(mXform);

            var markerData = new CartographerRadarMarkerData
            {
                Position = worldPos,
                Color = mComp.Color,
                Size = mComp.Size,
                ZoomScaling = mComp.ZoomScaling,
                RsiPath = mComp.RsiPath,
                State = mComp.State
            };

            markersToSend.Add(markerData);
        }

        var state = new MedievalCartographerBoundUserInterfaceState(
            netCoordinates,
            angle.Value,
            maxRange,
            rotateWithEntity,
            markersToSend
        );

        _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, state);
    }
}
