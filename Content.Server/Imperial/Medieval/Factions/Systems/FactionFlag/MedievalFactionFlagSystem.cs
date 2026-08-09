using Content.Server.Imperial.Medieval.CapturePoint;
using Content.Shared.Imperial.Medieval.CapturePoint.Components;
using Content.Shared.Imperial.Medieval.FactionFlag.System;
using Content.Shared.Imperial.Medieval.FactionFlag.UI;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.Factions.Systems.FactionFlag;

public sealed partial class MedievalFactionFlagSystem : SharedMedievalFactionFlagSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly CapturePointSystem _capturePoint = default!;

    private float _updateTimer;
    private readonly float _updateInterval = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalFactionFlagComponent, InteractHandEvent>(OnInteractHand);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < _updateInterval)
            return;
        _updateTimer -= _updateInterval;

        var query = EntityQueryEnumerator<MedievalFactionFlagComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out _, out var uiComp))
        {
            if (!_ui.IsUiOpen((uid, uiComp), MedievalFactionFlagUiKey.Key))
                continue;

            UpdateState(uid);
        }
    }

    private void OnInteractHand(EntityUid uid, MedievalFactionFlagComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        UpdateState(uid);
        _ui.TryOpenUi(uid, MedievalFactionFlagUiKey.Key, args.User);

        args.Handled = true;
    }

    private void UpdateState(Entity<UserInterfaceComponent?> uid)
    {
        var state = new MedievalFactionFlagBuiState(GetPoints());
        _ui.SetUiState(uid, MedievalFactionFlagUiKey.Key, state);
    }

    private List<MedievalFactionFlagPointData> GetPoints()
    {
        var points = new List<MedievalFactionFlagPointData>();

        var query = EntityQueryEnumerator<CapturePointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            if (point.LocalMapPosition == null)
                continue;

            var captureRemaining = _capturePoint.GetCaptureRemaining((uid, point));
            var cooldownRemaining = _capturePoint.GetCooldownRemaining((uid, point));

            points.Add(new(
                GetNetEntity(uid),
                point.PointName,
                point.LocalMapPosition.Value,
                point.State,
                point.OwningFaction,
                point.CapturingFaction,
                captureRemaining,
                cooldownRemaining));
        }

        return points;
    }
}
