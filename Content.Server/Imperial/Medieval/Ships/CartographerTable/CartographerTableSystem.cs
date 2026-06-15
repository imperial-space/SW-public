using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Medieval.CartographerTable;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

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

        SubscribeLocalEvent<RadarConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;

        _updateTimer -= UpdateInterval;

        var query = EntityQueryEnumerator<RadarConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tableComp, out var xform))
        {
            if (_uiSystem.GetActors(uid, RadarConsoleUiKey.Key).Count() == 0)
                continue;

            UpdateTableInterface(uid, tableComp, xform);
        }
    }

    private void OnUiOpened(EntityUid uid, RadarConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey.Equals(RadarConsoleUiKey.Key))
        {
            var xform = Transform(uid);
            UpdateTableInterface(uid, component, xform);
        }
    }

    private void UpdateTableInterface(EntityUid uid, RadarConsoleComponent radarComp, TransformComponent xform)
    {
        if (xform.MapID == MapId.Nullspace)
            return;

        var coordinates = GetNetCoordinates(_transform.GetMoverCoordinates(uid, xform));
        var angle = _transform.GetWorldRotation(xform);

        float maxRange = radarComp.MaxRange;
        bool rotateWithEntity = true;

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
            coordinates,
            angle,
            maxRange,
            rotateWithEntity,
            markersToSend
        );

        _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, state);
    }
}
