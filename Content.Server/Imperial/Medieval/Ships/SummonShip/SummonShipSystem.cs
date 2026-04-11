using System.IO;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Timing;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.SummonShip;

/// <summary>
/// This handles...
/// </summary>
public sealed class SummonShipSystem : EntitySystem
{

    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly UseDelaySystem _delay = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SummonShipComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, SummonShipComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var time = TimeSpan.FromSeconds(comp.Delay);
        _delay.SetLength(uid, time);
        Timer.Spawn(time, () => Use(uid, comp));
    }

    private void Use(EntityUid uid, SummonShipComponent comp)
    {
        var mapId = _transform.GetMapId(uid);
        var worldPos = _transform.GetWorldPosition(uid);

        var path = new ResPath(comp.File);

        if (!_mapLoader.TryLoadGrid(mapId, path, out var grid, offset: worldPos))
        {
            _adminLog.Add(LogType.Action, LogImpact.High, $"Ошибка Загрузки грида из {path} сущность загрузки {uid}");
            return;
        }
        
        EntityManager.QueueDeleteEntity(uid);
    }
}
