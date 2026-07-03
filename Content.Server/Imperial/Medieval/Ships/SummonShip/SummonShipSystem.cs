using System;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.SummonShip;

public sealed class SummonShipSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SummonShipComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, SummonShipComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var delay = TimeSpan.FromSeconds(comp.Delay);
        _delay.SetLength(uid, delay);
        Timer.Spawn(delay, () => Use(uid, comp));
    }

    private void Use(EntityUid uid, SummonShipComponent comp)
    {
        var mapId = _transform.GetMapId(uid);
        var worldPos = _transform.GetWorldPosition(uid);
        var path = new ResPath(comp.File);

        if (!_mapLoader.TryLoadGrid(mapId, path, out var grid, offset: worldPos))
        {
            _adminLog.Add(LogType.Action, LogImpact.High, $"РћС€РёР±РєР° Р—Р°РіСЂСѓР·РєРё РіСЂРёРґР° РёР· {path} СЃСѓС‰РЅРѕСЃС‚СЊ Р·Р°РіСЂСѓР·РєРё {uid}");
            return;
        }

        if (grid != null)
            EnsureComp<ShipDrowningComponent>(grid.Value);

        EntityManager.QueueDeleteEntity(uid);
    }
}
